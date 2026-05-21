using FuncPipeline;
using M.EventBrokerSlim.DependencyInjection;
using M.EventBrokerSlim.Persistent;
using Microsoft.Extensions.DependencyInjection;
using LiteDbIntegrationTests;

namespace M.EventBrokerSlim.PersistentEvents.LiteDb.Tests.Tests;

public class AbandonTest : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScope _scope;
    private readonly Setup _setup;

    public AbandonTest(Setup setup)
    {
        var builder = PipelineBuilder
            .Create()
            .NewPipeline()
            .Execute(async (SampleEvent e, IRetryPolicy retryPolicy, EventReceiver r, EventRecord record) =>
            {
                r.Add(record);
                if(record.RetryAttemptCount <= 1)
                {
                    retryPolicy.RetryAfter(TimeSpan.FromSeconds(1));
                }
                else
                {
                    retryPolicy.Abandon();
                }
            })
            .Build();
        var services = new ServiceCollection()
            .AddEventBroker(x => x.WithLiteDbPersistence((db, cfg) =>
            {
                cfg.PollingInterval = TimeSpan.FromSeconds(1);

                db.LiteDbInstance = setup.Database;
                db.Collection = nameof(AbandonTest);
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
    public async Task Event_retried_and_abandoned()
    {
        var broker = _scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var receiver = _scope.ServiceProvider.GetRequiredService<EventReceiver>();
        var sampleEvent = new SampleEvent("retry and dead-letter");

        await broker.Publish(sampleEvent, TestContext.Current.CancellationToken);

        await receiver.WaitForEventsAsync(3, TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var receivedEvents = receiver.GetReceivedEvents();
        var receivedEvent = Assert.Single(receivedEvents, x => x.Event is SampleEvent && x.EventRecord.RetryAttemptCount == 0);
        Assert.Equal(sampleEvent, receivedEvent.Event);
        receivedEvent = Assert.Single(receivedEvents, x => x.Event is SampleEvent && x.EventRecord.RetryAttemptCount == 1);
        Assert.Equal(sampleEvent, receivedEvent.Event);
        receivedEvent = Assert.Single(receivedEvents, x => x.Event is SampleEvent && x.EventRecord.RetryAttemptCount == 2);
        Assert.Equal(sampleEvent, receivedEvent.Event);
        AssertDeadLetteredStatus(receivedEvent.EventRecord);
    }

    private void AssertDeadLetteredStatus(EventRecord eventRecord)
    {
        var col = _setup.Database.GetCollection(nameof(AbandonTest));
        var doc = col.FindById(long.Parse(eventRecord.Id));
        Assert.NotNull(doc);
        Assert.Equal((int)EventStatus.DeadLettered, doc["Status"].AsInt32);
    }

    public void Dispose()
    {
        _scope.ServiceProvider.GetRequiredService<IEventBroker>().Shutdown();
        _scope.Dispose();
        _serviceProvider.Dispose();
    }
}
