using FuncPipeline;
using M.EventBrokerSlim.DependencyInjection;
using M.EventBrokerSlim.Persistent;
using Microsoft.Extensions.DependencyInjection;
using RedisIntegrationTests;

namespace M.EventBrokerSlim.PersistentEvents.Redis.Tests.Tests;

public class DeferredEventTest : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScope _scope;
    private readonly Setup _setup;

    public DeferredEventTest(Setup setup)
    {
        var builder = PipelineBuilder
            .Create()
            .NewPipeline()
            .Execute(async (SampleEvent e, EventReceiver r, EventRecord record) => r.Add(record))
            .Build();
        var services = new ServiceCollection()
            .AddEventBroker(x => x.WithRedisPersistence((db, cfg) =>
            {
                cfg.PollingInterval = TimeSpan.FromSeconds(1);

                db.ConnectionString = setup.ConnectionString;
                db.KeyPrefix = "ebs_2";
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
    public async Task Event_sent_and_received()
    {
        var broker = _scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var receiver = _scope.ServiceProvider.GetRequiredService<EventReceiver>();

        var event1 = new SampleEvent("event 1");

        await broker.PublishDeferred(event1, TimeSpan.FromSeconds(2));

        await receiver.WaitForEventsAsync(1, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var receivedEvents = receiver.GetReceivedEvents();
        var receivedEvent = Assert.Single(receivedEvents, e => e.Event is SampleEvent se && se.Message == "event 1");
        Assert.Equal(event1, receivedEvent.Event);
        await Assert.StatusIsAsync(EventStatus.Completed, receivedEvent.EventRecord.Id, _setup.DataSource.GetDatabase());
    }

    public void Dispose()
    {
        _scope.ServiceProvider.GetRequiredService<IEventBroker>().Shutdown();
        _scope.Dispose();
        _serviceProvider.Dispose();
    }
}

