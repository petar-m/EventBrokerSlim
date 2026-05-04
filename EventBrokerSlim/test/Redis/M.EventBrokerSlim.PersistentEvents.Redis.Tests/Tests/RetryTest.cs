using FuncPipeline;
using M.EventBrokerSlim.DependencyInjection;
using M.EventBrokerSlim.Persistent;
using Microsoft.Extensions.DependencyInjection;
using RedisIntegrationTests;
using YamlDotNet.Core.Tokens;

namespace M.EventBrokerSlim.PersistentEvents.Redis.Tests.Tests;

public class RetryTest : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScope _scope;
    private readonly Setup _setup;

    public RetryTest(Setup setup)
    {
        _setup = setup;
        var builder = PipelineBuilder
            .Create()
            .NewPipeline()
            .Execute(async (SampleEvent e, IRetryPolicy retryPolicy, EventReceiver r, EventRecord record) =>
            {
                r.Add(record);
                if(record.RetryAttemptCount <= 2)
                {
                    retryPolicy.RetryAfter(TimeSpan.FromSeconds(1));
                }
            })
            .Build();
        var services = new ServiceCollection()
            .AddEventBroker(x => x.WithRedisPersistence((db, cfg) =>
            {
                cfg.PollingInterval = TimeSpan.FromSeconds(1);

                db.ConnectionString = setup.ConnectionString;
                db.KeyPrefix = "ebs_5";
            }))
            .AddEventHandlerPipeline<SampleEvent>(builder.Pipelines[0], o => o.WithHandlerName("sample-event-handler"))
            .AddSingleton(EventRegistryHelper.Registry)
            .AddSingleton<EventReceiver>();

        _serviceProvider = services.BuildServiceProvider();
        _serviceProvider.UsePersistentEventBroker();
        _scope = _serviceProvider.CreateScope();
    }

    [Fact]
    public async Task Event_retried_and_successfully_handled()
    {
        var broker = _scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var receiver = _scope.ServiceProvider.GetRequiredService<EventReceiver>();
        var sampleEvent = new SampleEvent("retry");

        await broker.Publish(sampleEvent, TestContext.Current.CancellationToken);

        await receiver.WaitForEventsAsync(4, TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var receivedEvents = receiver.GetReceivedEvents();
        var receivedEvent = Assert.Single(receivedEvents, x => x.Event is SampleEvent && x.EventRecord.RetryAttemptCount == 0);
        Assert.Equal(sampleEvent, receivedEvent.Event);
        receivedEvent = Assert.Single(receivedEvents, x => x.Event is SampleEvent && x.EventRecord.RetryAttemptCount == 1);
        Assert.Equal(sampleEvent, receivedEvent.Event);
        receivedEvent = Assert.Single(receivedEvents, x => x.Event is SampleEvent && x.EventRecord.RetryAttemptCount == 2);
        Assert.Equal(sampleEvent, receivedEvent.Event);
        receivedEvent = Assert.Single(receivedEvents, x => x.Event is SampleEvent && x.EventRecord.RetryAttemptCount == 3);
        Assert.Equal(sampleEvent, receivedEvent.Event);
        await Assert.StatusIsAsync(EventStatus.Completed, receivedEvent.EventRecord.Id, _setup.DataSource.GetDatabase());
    }

    public void Dispose()
    {
        _scope.ServiceProvider.GetRequiredService<IEventBroker>().Shutdown();
        _scope.Dispose();
        _serviceProvider.Dispose();
    }
}
