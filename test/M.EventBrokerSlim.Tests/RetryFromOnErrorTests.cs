namespace M.EventBrokerSlim.Tests;

public class RetryFromOnErrorTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task OnError_SingleRetry_RetriesOnce_With_GivenDelay(int maxConcurrentHandlers)
    {
        // Arrange
        var services = ServiceProviderHelper.Build(
            sc => sc.AddEventBroker(
                        x => x.WithMaxConcurrentHandlers(maxConcurrentHandlers)
                              .AddTransient<TestEvent, TestEventHandler>())
                    .AddSingleton(new HandlerSettings(RetryAttempts: 1, Delay: TimeSpan.FromMilliseconds(100)))
                    .AddSingleton<EventsTracker>());

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsTracker = scope.ServiceProvider.GetRequiredService<EventsTracker>();
        var event1 = new TestEvent("test");

        // Act
        await eventBroker.Publish(event1);
        await eventsTracker.Wait(TimeSpan.FromSeconds(1));

        // Assert
        Assert.Equal(2, eventsTracker.Items.Count);
        var timestamps = eventsTracker.Items.OrderBy(x => x.Timestamp).Select(x => x.Timestamp).ToArray();
        Assert.Equal(100, (timestamps[1] - timestamps[0]).TotalMilliseconds, tolerance: 60);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task OnError_MultipleRetries_RetriesMultipleTimes_With_GivenDelay(int maxConcurrentHandlers)
    {
        // Arrange
        var services = ServiceProviderHelper.Build(
            sc => sc.AddEventBroker(
                        x => x.WithMaxConcurrentHandlers(maxConcurrentHandlers)
                              .AddTransient<TestEvent, TestEventHandler>())
                    .AddSingleton(new HandlerSettings(RetryAttempts: 3, Delay: TimeSpan.FromMilliseconds(150)))
                    .AddSingleton<EventsTracker>());

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsTracker = scope.ServiceProvider.GetRequiredService<EventsTracker>();
        var event1 = new TestEvent("test");

        // Act
        await eventBroker.Publish(event1);
        await eventsTracker.Wait(TimeSpan.FromSeconds(1));

        // Assert
        Assert.Equal(4, eventsTracker.Items.Count);
        var timestamps = eventsTracker.Items.OrderBy(x => x.Timestamp).Select(x => x.Timestamp).ToArray();
        Assert.Equal(150, (timestamps[1] - timestamps[0]).TotalMilliseconds, tolerance: 60);
        Assert.Equal(150, (timestamps[2] - timestamps[1]).TotalMilliseconds, tolerance: 60);
        Assert.Equal(150, (timestamps[3] - timestamps[2]).TotalMilliseconds, tolerance: 60);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task OnError_MultipleRetries_RetriesMultipleTimes_With_ZeroDelay(int maxConcurrentHandlers)
    {
        // Arrange
        var services = ServiceProviderHelper.Build(
            sc => sc.AddEventBroker(
                        x => x.WithMaxConcurrentHandlers(maxConcurrentHandlers)
                              .AddTransient<TestEvent, TestEventHandler>())
                    .AddSingleton(new HandlerSettings(RetryAttempts: 3, Delay: TimeSpan.Zero))
                    .AddSingleton<EventsTracker>());

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsTracker = scope.ServiceProvider.GetRequiredService<EventsTracker>();
        var event1 = new TestEvent("test");

        // Act
        await eventBroker.Publish(event1);
        await eventsTracker.Wait(TimeSpan.FromSeconds(1));

        // Assert
        Assert.Equal(4, eventsTracker.Items.Count);
        var timestamps = eventsTracker.Items.OrderBy(x => x.Timestamp).Select(x => x.Timestamp).ToArray();
        Assert.Equal(0, (timestamps[1] - timestamps[0]).TotalMilliseconds, tolerance: 60);
        Assert.Equal(0, (timestamps[2] - timestamps[1]).TotalMilliseconds, tolerance: 60);
        Assert.Equal(0, (timestamps[3] - timestamps[2]).TotalMilliseconds, tolerance: 60);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task OnError_MultipleRetries_Event_IsTheSameInstance_EveryTime(int maxConcurrentHandlers)
    {
        // Arrange
        var services = ServiceProviderHelper.Build(
            sc => sc.AddEventBroker(
                        x => x.WithMaxConcurrentHandlers(maxConcurrentHandlers)
                              .AddTransient<TestEvent, TestEventHandler>())
                    .AddSingleton(new HandlerSettings(RetryAttempts: 3, Delay: TimeSpan.FromMilliseconds(150)))
                    .AddSingleton<EventsTracker>());

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsTracker = scope.ServiceProvider.GetRequiredService<EventsTracker>();
        var event1 = new TestEvent("test");

        // Act
        await eventBroker.Publish(event1);
        await eventsTracker.Wait(TimeSpan.FromSeconds(1));

        // Assert
        Assert.Equal(4, eventsTracker.Items.Count);
        Assert.All(eventsTracker.Items.Select(x => x.Item), x => Assert.Same(event1, x));
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

        public Task Handle(TestEvent @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task OnError(Exception exception, TestEvent @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken)
        {
            _tracker.Track(@event);
            if(retryPolicy.Attempt < _settings.RetryAttempts)
            {
                retryPolicy.RetryAfter(_settings.Delay);
            }

            return Task.CompletedTask;
        }
    }
}
