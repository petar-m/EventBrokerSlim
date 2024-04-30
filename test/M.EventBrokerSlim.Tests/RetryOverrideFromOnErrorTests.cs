namespace M.EventBrokerSlim.Tests;

public class RetryOverrideFromOnErrorTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task OnError_Overrides_Handle_RetryPolicy_Delay(int maxConcurrentHandlers)
    {
        // Arrange
        var services = ServiceProviderHelper.Build(
            sc => sc.AddEventBroker(
                        x => x.WithMaxConcurrentHandlers(maxConcurrentHandlers)
                              .AddKeyedTransient<TestEvent, TestEventHandler>())
                    .AddSingleton(new HandlerSettings(RetryAttempts: 1, Delay: TimeSpan.FromMilliseconds(100)))
                    .AddSingleton<EventsTracker>());

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsTracker = scope.ServiceProvider.GetRequiredService<EventsTracker>();
        var event1 = new TestEvent("test");

        // Act
        await eventBroker.Publish(event1);
        await eventsTracker.Wait(TimeSpan.FromMilliseconds(500));

        // Assert
        Assert.Equal(2, eventsTracker.Items.Count);
        var timestamps = eventsTracker.Items.OrderBy(x => x.Timestamp).Select(x => x.Timestamp).ToArray();
        Assert.Equal(400, (timestamps[1] - timestamps[0]).TotalMilliseconds, tolerance: 60);
    }

    public class TestEvent(string Info)
    {
        public string Info { get; } = Info;
    }

    public record HandlerSettings(int RetryAttempts, TimeSpan Delay);

    public class TestEventHandler : IEventHandler<TestEvent>
    {
        private readonly Random _random = new();
        private readonly EventsTracker _tracker;
        private readonly HandlerSettings _settings;

        public TestEventHandler(HandlerSettings settings, EventsTracker tracker)
        {
            _settings = settings;
            _tracker = tracker;
        }

        public async Task Handle(TestEvent @event, RetryPolicy retryPolicy, CancellationToken cancellationToken)
        {
            await Task.Delay(_random.Next(1, 10));
            if(retryPolicy.Attempt < _settings.RetryAttempts)
            {
                retryPolicy.RetryAfter(_settings.Delay);
            }

            throw new NotImplementedException();
        }

        public Task OnError(Exception exception, TestEvent @event, RetryPolicy retryPolicy, CancellationToken cancellationToken)
        {
            _tracker.Track(@event);
            if(retryPolicy.Attempt < _settings.RetryAttempts)
            {
                retryPolicy.RetryAfter(_settings.Delay.Add(TimeSpan.FromMilliseconds(300)));
            }

            return Task.CompletedTask;
        }
    }
}
