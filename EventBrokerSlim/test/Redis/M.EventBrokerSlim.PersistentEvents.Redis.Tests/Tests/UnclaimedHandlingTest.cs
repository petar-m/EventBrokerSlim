using M.EventBrokerSlim.DependencyInjection;
using M.EventBrokerSlim.Persistent;
using Microsoft.Extensions.DependencyInjection;
using RedisIntegrationTests;

namespace M.EventBrokerSlim.PersistentEvents.Redis.Tests.Tests;

public class UnclaimedHandlingTest : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScope _scope;
    private readonly Setup _setup;

    public UnclaimedHandlingTest(Setup setup)
    {
        _setup = setup;
        var services = new ServiceCollection()
            .AddEventBroker(x => x.WithRedisPersistence((db, cfg) =>
            {
                cfg.UnclaimedTtl = TimeSpan.FromSeconds(2);
                cfg.DeadLetterUnclaimedExecuteInterval =
                new Jitter(
                    initial: new Jitter.Periodic(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(0)),
                    regular: new Jitter.Periodic(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)));

                db.ConnectionString = setup.ConnectionString;
                db.KeyPrefix = "ebs_9";
            }))
            .AddSingleton(EventRegistryHelper.Registry);

        services.AddEventHandlerPipeline<SampleEvent>(NullPipeline.Instance, handlerName: "null-pipeline");

        _serviceProvider = services.BuildServiceProvider();
        _scope = _serviceProvider.CreateScope();
        _scope.ServiceProvider.UsePersistentEventBroker();
    }

    [Fact]
    public async Task Scheduled_event_exceeding_unclaimed_TTL_is_dead_lettered()
    {
        var eventBroker = _scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var sampleEvent = new SampleEvent("unclaimed");

        await eventBroker.Publish(sampleEvent, TestContext.Current.CancellationToken);

        // give room to maintenance task to dead-letter the event
        await Task.Delay(TimeSpan.FromSeconds(4), TestContext.Current.CancellationToken);
        var server = _setup.DataSource.GetServers().Single();
        var keys = server.Keys(pattern: "{ebs_9}:evt:*");
        var key = Assert.Single(keys);
        await Assert.StatusIsAsync(EventStatus.DeadLettered, key!, _setup.DataSource.GetDatabase());
    }

    public void Dispose()
    {
        _serviceProvider.GetRequiredService<IEventBroker>().Shutdown();
        _serviceProvider.Dispose();
    }
}
