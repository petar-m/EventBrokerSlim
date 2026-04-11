using FuncPipeline;
using M.EventBrokerSlim.DependencyInjection;
using M.EventBrokerSlim.Persistent;
using Microsoft.Extensions.DependencyInjection;
using PostgreSqlIntegrationTests;

namespace M.EventBrokerSlim.PersistentEvents.PostgreSql.Tests.Tests;

public class MultipleEventsTest : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScope _scope;

    public MultipleEventsTest(Setup setup)
    {
        var builder = PipelineBuilder
            .Create()
            .NewPipeline()
            .Execute(async (SampleEvent e, EventReceiver r, EventRecord record) => r.Add(record))
            .Build()
            .NewPipeline()
            .Execute(async (SampleEvent2 e, EventReceiver r, EventRecord record) => r.Add(record))
            .Build();
        var services = new ServiceCollection()
            .AddEventBroker(x => x.WithPostgreSqlPersistence((db, cfg) =>
            {
                cfg.PollingInterval = TimeSpan.FromSeconds(2);
                cfg.ScheduledBatchSize = 2;

                db.ConnectionString = setup.ConnectionString;
                db.Schema = "ebs_1";
                db.Table = nameof(MultipleEventsTest);
                db.CreateEventsTable();
            }))
            .AddEventHandlerPipeline<SampleEvent>(builder.Pipelines[0], o => o.WithHandlerName("sample-event-handler"))
            .AddEventHandlerPipeline<SampleEvent2>(builder.Pipelines[1], o => o.WithHandlerName("sample-event-handler-2"))
            .AddSingleton(EventRegistryHelper.Registry)
            .AddSingleton<EventReceiver>();

        _serviceProvider = services.BuildServiceProvider();
        _serviceProvider.UsePersistentEventBroker(throwOnValidationErrors: true);
        _scope = _serviceProvider.CreateScope();
    }

    [Fact]
    public async Task Event_sent_and_received()
    {
        var broker = _scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var receiver = _scope.ServiceProvider.GetRequiredService<EventReceiver>();

        var event1 = new SampleEvent("event 1");
        var event2 = new SampleEvent2(2, "event 2");
        var event3 = new SampleEvent("event 3");
        var event4 = new SampleEvent2(4, "event 4");

        await broker.Publish(event1, TestContext.Current.CancellationToken);
        await broker.Publish(event2, TestContext.Current.CancellationToken);
        await broker.Publish(event3, TestContext.Current.CancellationToken);
        await broker.Publish(event4, TestContext.Current.CancellationToken);

        await receiver.WaitForEventsAsync(4, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var receivedEvents = receiver.GetReceivedEvents();
        Assert.Equal(event1, receivedEvents.Single(x => x is SampleEvent e && e.Message == "event 1"));
        Assert.Equal(event2, receivedEvents.Single(x => x is SampleEvent2 e && e.Value == 2));
        Assert.Equal(event3, receivedEvents.Single(x => x is SampleEvent e && e.Message == "event 3"));
        Assert.Equal(event4, receivedEvents.Single(x => x is SampleEvent2 e && e.Value == 4));
    }

    public void Dispose()
    {
        _scope.ServiceProvider.GetRequiredService<IEventBroker>().Shutdown();
        _scope.Dispose();
        _serviceProvider.Dispose();
    }
}
