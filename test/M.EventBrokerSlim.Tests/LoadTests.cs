namespace M.EventBrokerSlim.Tests;

public class LoadTests
{
    [Fact]
    public async Task Load_MultipleHandlers_With_Retry()
    {
        // Arrange
        var services = ServiceProviderHelper.Build(
            sc => sc.AddEventBroker(
                        x => x.WithMaxConcurrentHandlers(5)
                              .AddTransient<Event1, TestEventHandler1<Event1>>()
                              .AddTransient<Event1, TestEventHandler2<Event1>>()
                              .AddTransient<Event1, TestEventHandler3<Event1>>()
                              .AddTransient<Event2, TestEventHandler1<Event2>>()
                              .AddTransient<Event2, TestEventHandler2<Event2>>()
                              .AddTransient<Event2, TestEventHandler3<Event2>>()
                              .AddTransient<Event3, TestEventHandler1<Event3>>()
                              .AddTransient<Event3, TestEventHandler2<Event3>>()
                              .AddTransient<Event3, TestEventHandler3<Event3>>())
                    .AddSingleton(new HandlerSettings(RetryAttempts: 3, Delay: TimeSpan.FromMilliseconds(100)))
                    .AddSingleton<EventsTracker>());

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsTracker = scope.ServiceProvider.GetRequiredService<EventsTracker>();

        const int EventsCount = 100_000;
        eventsTracker.ExpectedItemsCount = 3 * (3 * EventsCount + EventsCount / 250 * 3 + EventsCount / 500 * 3);

        // Act
        foreach(var i in Enumerable.Range(1, EventsCount))
        {
            await eventBroker.Publish(new Event1("event", i));
            await eventBroker.Publish(new Event2("event", i));
            await eventBroker.Publish(new Event3("event", i));
        }

        await eventsTracker.Wait(TimeSpan.FromSeconds(10));

        // Assert
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

    public class TestEventBase(string Info, int Number)
    {
        public string Info { get; } = Info;
        public int Number { get; } = Number;
    }

    public class Event1(string Info, int Number) : TestEventBase(Info, Number)
    {
    }

    public class Event2(string Info, int Number) : TestEventBase(Info, Number)
    {
    }

    public class Event3(string Info, int Number) : TestEventBase(Info, Number)
    {
    }

    public record HandlerSettings(int RetryAttempts, TimeSpan Delay);

    public class TestEventHandler1<T> : IEventHandler<T> where T : TestEventBase
    {
        private readonly EventsTracker _tracker;
        private readonly HandlerSettings _settings;

        public TestEventHandler1(HandlerSettings settings, EventsTracker tracker)
        {
            _settings = settings;
            _tracker = tracker;
        }

        public Task Handle(T @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken)
        {
            _tracker.Track(@event);
            if(@event.Number % 250 == 0)
            {
                throw new NotImplementedException();
            }
            return Task.CompletedTask;
        }

        public Task OnError(Exception exception, T @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken)
        {
            if(@event.Number % 250 == 0 && retryPolicy.Attempt < _settings.RetryAttempts)
            {
                retryPolicy.RetryAfter(_settings.Delay);
            }
            return Task.CompletedTask;
        }
    }

    public class TestEventHandler2<T> : IEventHandler<T> where T : TestEventBase
    {
        private readonly EventsTracker _tracker;
        private readonly HandlerSettings _settings;

        public TestEventHandler2(HandlerSettings settings, EventsTracker tracker)
        {
            _settings = settings;
            _tracker = tracker;
        }

        public Task Handle(T @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken)
        {
            _tracker.Track(@event);
            if(@event.Number % 500 == 0 && retryPolicy.Attempt < _settings.RetryAttempts)
            {
                retryPolicy.RetryAfter(_settings.Delay);
            }
            return Task.CompletedTask;
        }

        public Task OnError(Exception exception, T @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    public class TestEventHandler3<T> : IEventHandler<T>
    {
        private readonly EventsTracker _tracker;

        public TestEventHandler3(EventsTracker tracker)
        {
            _tracker = tracker;
        }

        public Task Handle(T @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken)
        {
            _tracker.Track(@event!);
            return Task.CompletedTask;
        }

        public Task OnError(Exception exception, T @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
