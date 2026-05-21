using LiteDB;
using M.EventBrokerSlim.DependencyInjection;
using M.EventBrokerSlim.Persistent;
using Microsoft.Extensions.DependencyInjection;
using LiteDbIntegrationTests;

namespace M.EventBrokerSlim.PersistentEvents.LiteDb.Tests.Tests;

public class UnclaimedHandlingTest : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScope _scope;
    private readonly Setup _setup;

    public UnclaimedHandlingTest(Setup setup)
    {
        var services = new ServiceCollection()
            .AddEventBroker(x => x.WithLiteDbPersistence((db, cfg) =>
            {
                cfg.UnclaimedTtl = TimeSpan.FromSeconds(2);
                cfg.DeadLetterUnclaimedExecuteInterval =
                new Jitter(
                    initial: new Jitter.Periodic(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(0)),
                    regular: new Jitter.Periodic(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)));

                db.LiteDbInstance = setup.Database;
                db.Collection = nameof(UnclaimedHandlingTest);
            }))
            .AddSingleton(EventRegistryHelper.Registry);

        services.AddEventHandlerPipeline<SampleEvent>(NullPipeline.Instance, handlerName: "null-pipeline");

        _serviceProvider = services.BuildServiceProvider();
        _scope = _serviceProvider.CreateScope();
        _scope.ServiceProvider.UsePersistentEventBroker();
        _setup = setup;
    }

    [Fact]
    public async Task Scheduled_event_exceeding_unclaimed_TTL_is_dead_lettered()
    {
        var eventBroker = _scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var sampleEvent = new SampleEvent("unclaimed");

        await eventBroker.Publish(sampleEvent, TestContext.Current.CancellationToken);

        // give room to maintenance task to dead-letter the event
        await Task.Delay(TimeSpan.FromSeconds(4), TestContext.Current.CancellationToken);
        AssertDeadLetterStatus();
    }

    private void AssertDeadLetterStatus()
    {
        var col = _setup.Database.GetCollection(nameof(UnclaimedHandlingTest));
        var docs = col.FindAll().ToList();
        Assert.Single(docs);
        Assert.Equal((int)EventStatus.DeadLettered, docs[0]["Status"].AsInt32);
    }

    public void Dispose()
    {
        _serviceProvider.GetRequiredService<IEventBroker>().Shutdown();
        _scope.Dispose();
        _serviceProvider.Dispose();
    }
}
