namespace M.EventBrokerSlim.Tests;

public class OrderOfRetriesTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Retries_ExecutedInCorrectOrder_RespectingDelays(int maxConcurrentHandlers)
    {
        // Arrange
        var services = ServiceProviderHelper.Build(
            sc => sc.AddEventBroker(
                        x => x.WithMaxConcurrentHandlers(maxConcurrentHandlers)
                              .AddKeyedTransient<TestEvent1, TestEventHandler1>()
                              .AddKeyedTransient<TestEvent2, TestEventHandler2>())
                    .AddSingleton<EventsTracker>());

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsTracker = scope.ServiceProvider.GetRequiredService<EventsTracker>();
        var event1 = new TestEvent1("test");
        var event2 = new TestEvent2("test");

        // Act
        await eventBroker.Publish(event1);
        await Task.Delay(TimeSpan.FromMilliseconds(100));
        await eventBroker.Publish(event2);

        await eventsTracker.Wait(TimeSpan.FromSeconds(1));

        // Assert
        Assert.Equal(5, eventsTracker.Items.Count);
        var eventsByTimeHandled = eventsTracker.Items.OrderBy(x => x.Timestamp).Select(x => x.Event).ToArray();
        Assert.Equal(eventsByTimeHandled[0], event1);
        Assert.Equal(eventsByTimeHandled[1], event2);
        Assert.Equal(eventsByTimeHandled[2], event2);
        Assert.Equal(eventsByTimeHandled[3], event2);
        Assert.Equal(eventsByTimeHandled[4], event1);
    }

    public class TestEvent1(string Info)
    {
        public string Info { get; } = Info;
    }

    public class TestEvent2(string Info)
    {
        public string Info { get; } = Info;
    }

    public record HandlerSettings(int RetryAttempts, TimeSpan Delay);

    public class TestEventHandler1 : IEventHandler<TestEvent1>
    {
        private readonly EventsTracker _tracker;

        public TestEventHandler1(EventsTracker tracker)
        {
            _tracker = tracker;
        }

        public Task Handle(TestEvent1 @event, RetryPolicy retryPolicy, CancellationToken cancellationToken)
        {
            _tracker.Track(@event);
            if(retryPolicy.Attempt < 1)
            {
                retryPolicy.RetryAfter(TimeSpan.FromMilliseconds(800));
            }
            throw new NotImplementedException();
        }

        public Task OnError(Exception exception, TestEvent1 @event, RetryPolicy retryPolicy, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    public class TestEventHandler2 : IEventHandler<TestEvent2>
    {
        private readonly EventsTracker _tracker;

        public TestEventHandler2(EventsTracker tracker)
        {
            _tracker = tracker;
        }

        public Task Handle(TestEvent2 @event, RetryPolicy retryPolicy, CancellationToken cancellationToken)
        {
            _tracker.Track(@event);
            if(retryPolicy.Attempt < 2)
            {
                retryPolicy.RetryAfter(TimeSpan.FromMilliseconds(100));
            }
            throw new NotImplementedException();
        }

        public Task OnError(Exception exception, TestEvent2 @event, RetryPolicy retryPolicy, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
