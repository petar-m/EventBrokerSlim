using FuncPipeline;
using M.EventBrokerSlim.DependencyInjection;
using M.EventBrokerSlim.Persistent;
using Microsoft.Extensions.DependencyInjection;
using LiteDbIntegrationTests;

namespace M.EventBrokerSlim.PersistentEvents.LiteDb.Tests.Tests;

public class RescheduleTest : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScope _scope;
    private readonly Setup _setup;

    public RescheduleTest(Setup setup)
    {
        var builder = PipelineBuilder
            .Create()
            .NewPipeline()
            .Execute(async (EventRecord er, EventReceiver receiver, CancellationToken cancellationToken) =>
            {
                receiver.Add(er);
                await Task.Delay(TimeSpan.FromHours(1), cancellationToken);
            })
            .Build();
        var services = new ServiceCollection()
            .AddEventBroker(x => x.WithLiteDbPersistence((db, cfg) =>
            {
                cfg.PollingInterval = TimeSpan.FromHours(1);
                cfg.ProcessingTimeout = TimeSpan.FromSeconds(1);
                cfg.RescheduleClaimedExceedingProcessingTimeoutExecuteInterval =
                new Jitter(
                    initial: new Jitter.Periodic(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(0)),
                    regular: new Jitter.Periodic(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)));

                db.LiteDbInstance = setup.Database;
                db.Collection = nameof(RescheduleTest);
            }))
            .AddEventHandlerPipeline<SampleEvent>(builder.Pipelines[0], o => o.WithHandlerName("sample-event-handler"))
            .AddSingleton(EventRegistryHelper.Registry)
            .AddSingleton<EventReceiver>();

        _serviceProvider = services.BuildServiceProvider();
        _serviceProvider.UsePersistentEventBroker();
        _scope = _serviceProvider.CreateScope();
        _setup = setup;
    }

    [Fact]
    public async Task Event_exceeding_processing_timeout_is_rescheduled()
    {
        var broker = _scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var receiver = _scope.ServiceProvider.GetRequiredService<EventReceiver>();
        var sampleEvent = new SampleEvent("to be rescheduled");

        await broker.Publish(sampleEvent, TestContext.Current.CancellationToken);

        await receiver.WaitForEventsAsync(1, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var receivedEvents = receiver.GetReceivedEvents();
        var receivedEvent = Assert.Single(receivedEvents);
        Assert.Equal(sampleEvent, receivedEvent.Event);
        Assert.Equal(EventStatus.InProgress, receivedEvent.EventRecord.Status);
        // give room to maintenance task to reschedule the event
        await Task.Delay(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        AssertScheduledStatus(receivedEvent.EventRecord);
    }

    private void AssertScheduledStatus(EventRecord eventRecord)
    {
        var col = _setup.Database.GetCollection(nameof(RescheduleTest));
        var doc = col.FindById(long.Parse(eventRecord.Id));
        Assert.NotNull(doc);
        Assert.Equal((int)EventStatus.Scheduled, doc["Status"].AsInt32);
    }

    public void Dispose()
    {
        _scope.ServiceProvider.GetRequiredService<IEventBroker>().Shutdown();
        _scope.Dispose();
        _serviceProvider.Dispose();
    }
}
