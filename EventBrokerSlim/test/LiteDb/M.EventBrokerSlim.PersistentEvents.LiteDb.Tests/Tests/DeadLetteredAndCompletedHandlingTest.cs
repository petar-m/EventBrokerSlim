using FuncPipeline;
using M.EventBrokerSlim.DependencyInjection;
using M.EventBrokerSlim.Persistent;
using Microsoft.Extensions.DependencyInjection;
using LiteDbIntegrationTests;

namespace M.EventBrokerSlim.PersistentEvents.LiteDb.Tests.Tests;

public class DeadLetteredAndCompletedHandlingTest : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScope _scope;
    private readonly Setup _setup;

    public DeadLetteredAndCompletedHandlingTest(Setup setup)
    {
        var services = new ServiceCollection()
            .AddEventBroker(x => x.WithLiteDbPersistence((db, cfg) =>
            {
                cfg.CompletedRecordTtl = TimeSpan.FromSeconds(2);
                cfg.DeadLetteredRecordTtl = TimeSpan.FromSeconds(2);
                cfg.DeleteCompletedAndDeadLetteredExceedingTtlExecuteInterval =
                new Jitter(
                    initial: new Jitter.Periodic(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(0)),
                    regular: new Jitter.Periodic(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)));

                db.LiteDbInstance = setup.Database;
                db.Collection = nameof(DeadLetteredAndCompletedHandlingTest);
            }))
            .AddSingleton(EventRegistryHelper.Registry);

        var builder = PipelineBuilder.Create()
            .NewPipeline()
            .Execute(() => Task.CompletedTask) // produces status completed
            .Build()
            .NewPipeline()
            .Execute((IRetryPolicy retryPolicy) => // produces status dead lettered
            {
                retryPolicy.Abandon();
                return Task.CompletedTask;
            })
            .Build();

        services.AddEventHandlerPipeline<SampleEvent>(builder.Pipelines[0], handlerName: "handler-1");
        services.AddEventHandlerPipeline<SampleEvent>(builder.Pipelines[1], handlerName: "handler-2");

        _serviceProvider = services.BuildServiceProvider();
        _scope = _serviceProvider.CreateScope();
        _scope.ServiceProvider.UsePersistentEventBroker();
        _setup = setup;
    }

    [Fact]
    public async Task Dead_lettered_and_completed_events_exceeding_TTL_are_deleted()
    {
        var eventBroker = _scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var sampleEvent = new SampleEvent("two handlers");

        await eventBroker.Publish(sampleEvent, TestContext.Current.CancellationToken);

        // give room to maintenance task to delete the events
        await Task.Delay(TimeSpan.FromSeconds(4), TestContext.Current.CancellationToken);
        AssertNoDocuments();
    }

    private void AssertNoDocuments()
    {
        var col = _setup.Database.GetCollection(nameof(DeadLetteredAndCompletedHandlingTest));
        var count = col.Count();
        Assert.Equal(0, count);
    }

    public void Dispose()
    {
        _scope.ServiceProvider.GetRequiredService<IEventBroker>().Shutdown();
        _scope.Dispose();
        _serviceProvider.Dispose();
    }
}
