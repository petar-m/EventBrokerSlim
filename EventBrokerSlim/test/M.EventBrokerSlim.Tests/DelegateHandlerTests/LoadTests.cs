using FuncPipeline;
using MELT;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace M.EventBrokerSlim.Tests.DelegateHandlerTests;

public class LoadTests
{
    private readonly ITestOutputHelper _output;

    public LoadTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Load_MultipleDelegateHandlers_With_Retry()
    {
        // Arrange
        var serviceCollection = new ServiceCollection()
            .AddEventBroker(x => x.WithMaxConcurrentHandlers(5))
            .AddSingleton(new HandlerSettings(RetryAttempts: 3, Delay: TimeSpan.FromMilliseconds(100)))
            .AddLogging(x => x.AddTest())
            .AddSingleton<EventsTracker>();

        PipelineBuilder.Create()
            .NewPipeline()
            .Execute<Event1, INext, HandlerSettings, IRetryPolicy, EventsTracker>(DelegateEventHandlers.TestEventHandler1ErrorHandler<Event1>)
            .Execute<Event1, EventsTracker>(DelegateEventHandlers.TestEventHandler1<Event1>)
            .Build(x => serviceCollection.AddEventHandlerPileline<Event1>(x))
            .NewPipeline()
            .Execute<Event2, INext, HandlerSettings, IRetryPolicy, EventsTracker>(DelegateEventHandlers.TestEventHandler1ErrorHandler<Event2>)
            .Execute<Event2, EventsTracker>(DelegateEventHandlers.TestEventHandler1<Event2>)
            .Build(x => serviceCollection.AddEventHandlerPileline<Event2>(x))
            .NewPipeline()
            .Execute<Event3, INext, HandlerSettings, IRetryPolicy, EventsTracker>(DelegateEventHandlers.TestEventHandler1ErrorHandler<Event3>)
            .Execute<Event3, EventsTracker>(DelegateEventHandlers.TestEventHandler1<Event3>)
            .Build(x => serviceCollection.AddEventHandlerPileline<Event3>(x))
            .NewPipeline()
            .Execute<Event1, IRetryPolicy, EventsTracker, HandlerSettings>(DelegateEventHandlers.TestEventHandler2)
            .Build(x => serviceCollection.AddEventHandlerPileline<Event1>(x))
            .NewPipeline()
            .Execute<Event2, IRetryPolicy, EventsTracker, HandlerSettings>(DelegateEventHandlers.TestEventHandler2)
            .Build(x => serviceCollection.AddEventHandlerPileline<Event2>(x))
            .NewPipeline()
            .Execute<Event3, IRetryPolicy, EventsTracker, HandlerSettings>(DelegateEventHandlers.TestEventHandler2)
            .Build(x => serviceCollection.AddEventHandlerPileline<Event3>(x))
            .NewPipeline()
            .Execute<Event1, EventsTracker>(DelegateEventHandlers.TestEventHandler3)
            .Build(x => serviceCollection.AddEventHandlerPileline<Event1>(x))
            .NewPipeline()
            .Execute<Event2, EventsTracker>(DelegateEventHandlers.TestEventHandler3)
            .Build(x => serviceCollection.AddEventHandlerPileline<Event2>(x))
            .NewPipeline()
            .Execute<Event3, EventsTracker>(DelegateEventHandlers.TestEventHandler3)
            .Build(x => serviceCollection.AddEventHandlerPileline<Event3>(x)); ;

        using var services = serviceCollection.BuildServiceProvider(true);
        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsTracker = scope.ServiceProvider.GetRequiredService<EventsTracker>();

        const int EventsCount = 100_000;
        eventsTracker.ExpectedItemsCount = 3 * (3 * EventsCount + EventsCount / 250 * 3 + EventsCount / 500 * 3);

        // Act
        foreach(var i in Enumerable.Range(1, EventsCount))
        {
            await eventBroker.Publish(new Event1(i));
            await eventBroker.Publish(new Event2(i));
            await eventBroker.Publish(new Event3(i));
        }

        await eventsTracker.Wait(TimeSpan.FromSeconds(10));

        // Assert
        _output.WriteLine($"Processed: {eventsTracker.ExpectedItemsCount} events, Elapsed: {eventsTracker.Elapsed}");

        var provider = (TestLoggerProvider)scope.ServiceProvider.GetServices<ILoggerProvider>().Single(x => x is TestLoggerProvider);
        Assert.Empty(provider.Sink.LogEntries);

        var counters = eventsTracker.Items
            .Select(x => x.Item)
            .GroupBy(x => x.GetType())
            .Select(x => (Type: x.Key, Count: x.Count()))
        .ToArray();
        // 1 event, 3 handlers, one handler does not retry, other retries one each 250 events 3 times, other retries one each 500 events 3 times
        Assert.Equal(3 * EventsCount + EventsCount / 250 * 3 + EventsCount / 500 * 3, counters[0].Count);
        Assert.Equal(3 * EventsCount + EventsCount / 250 * 3 + EventsCount / 500 * 3, counters[1].Count);
        Assert.Equal(3 * EventsCount + EventsCount / 250 * 3 + EventsCount / 500 * 3, counters[2].Count);
    }

    public static class DelegateEventHandlers
    {
        public static Task TestEventHandler1<T>(T @event, EventsTracker tracker) where T : TestEventBase
        {
            tracker.Track(@event);
            if(@event.Number % 250 == 0)
            {
                throw new NotImplementedException();
            }

            return Task.CompletedTask;
        }

        public static async Task TestEventHandler1ErrorHandler<T>(T @event, INext nextHandler, HandlerSettings settings, IRetryPolicy retryPolicy, EventsTracker tracker) where T : TestEventBase
        {
            try
            {
                await nextHandler.RunAsync();
            }
            catch
            {
                if(@event.Number % 250 == 0 && retryPolicy.Attempt < settings.RetryAttempts)
                {
                    retryPolicy.RetryAfter(settings.Delay);
                }
            }
        }

        public static Task TestEventHandler2<T>(T @event, IRetryPolicy retryPolicy, EventsTracker tracker, HandlerSettings settings) where T : TestEventBase
        {
            tracker.Track(@event);
            if(@event.Number % 500 == 0 && retryPolicy.Attempt < settings.RetryAttempts)
            {
                retryPolicy.RetryAfter(settings.Delay);
            }

            return Task.CompletedTask;
        }

        public static Task TestEventHandler3<T>(T @event, EventsTracker tracker) where T : TestEventBase
        {
            tracker.Track(@event!);
            return Task.CompletedTask;
        }
    }
}
