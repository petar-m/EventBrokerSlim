using FuncPipeline;
using M.EventBrokerSlim.DependencyInjection;
using M.EventBrokerSlim.Persistent;
using Microsoft.Extensions.DependencyInjection;
using RedisIntegrationTests;

namespace M.EventBrokerSlim.PersistentEvents.Redis.Tests.Tests;

public class DeadLetteredAndCompletedHandlingTest : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly Setup _setup;
    private readonly IServiceScope _scope;

    public DeadLetteredAndCompletedHandlingTest(Setup setup)
    {
        _setup = setup;
        var services = new ServiceCollection()
            .AddEventBroker(x => x.WithRedisPersistence((db, cfg) =>
            {
                cfg.CompletedRecordTtl = TimeSpan.FromSeconds(2);
                cfg.DeadLetteredRecordTtl = TimeSpan.FromSeconds(2);
                cfg.DeleteCompletedAndDeadLetteredExceedingTtlExecuteInterval =
                new Jitter(
                    initial: new Jitter.Periodic(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(0)),
                    regular: new Jitter.Periodic(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)));

                db.ConnectionString = setup.ConnectionString;
                db.KeyPrefix = "ebs_10";
            }))
            .AddSingleton(EventRegistryHelper.Registry)
            .AddSingleton<EventReceiver>();

        var builder = PipelineBuilder.Create()
            .NewPipeline()
            .Execute((EventRecord eventRecord, EventReceiver receiver) =>
            {
                receiver.Add(eventRecord);
                // produces status completed
                return Task.CompletedTask;
            }) 
            .Build()
            .NewPipeline()
            .Execute((EventRecord eventRecord, EventReceiver receiver, IRetryPolicy retryPolicy) =>
            {
                receiver.Add(eventRecord);
                // produces status dead lettered
                retryPolicy.Abandon();
                return Task.CompletedTask;
            })
            .Build();

        services.AddEventHandlerPipeline<SampleEvent>(builder.Pipelines[0], handlerName: "handler-1");
        services.AddEventHandlerPipeline<SampleEvent>(builder.Pipelines[1], handlerName: "handler-2");

        _serviceProvider = services.BuildServiceProvider();
        _scope = _serviceProvider.CreateScope();
        _scope.ServiceProvider.UsePersistentEventBroker();
    }

    [Fact]
    public async Task Dead_lettered_and_completed_events_exceeding_TTL_are_deleted()
    {
        var eventBroker = _scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var receiver = _scope.ServiceProvider.GetRequiredService<EventReceiver>();
        var sampleEvent = new SampleEvent("two handlers");

        await eventBroker.Publish(sampleEvent, TestContext.Current.CancellationToken);

        // give room to maintenance task to delete the events
        await Task.Delay(TimeSpan.FromSeconds(4), TestContext.Current.CancellationToken);
        List<EventReceiver.ReceivedEvent> receivedEvents = receiver.GetReceivedEvents();
        Assert.Equal(2, receivedEvents.Count);
        var db = _setup.DataSource.GetDatabase();
        await Assert.KeyDoesNotExistAsync(receivedEvents[0].EventRecord.Id, db);
        await Assert.KeyDoesNotExistAsync(receivedEvents[1].EventRecord.Id, db);
    }

    public void Dispose()
    {
        _scope.ServiceProvider.GetRequiredService<IEventBroker>().Shutdown();
        _scope.Dispose();
        _serviceProvider.Dispose();
    }
}
