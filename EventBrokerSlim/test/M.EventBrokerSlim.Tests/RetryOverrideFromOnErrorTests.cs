﻿namespace M.EventBrokerSlim.Tests;

public class RetryOverrideFromOnErrorTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task OnError_Overrides_Handle_RetryPolicy_Delay(int maxConcurrentHandlers)
    {
        // Arrange
        var services = new ServiceCollection()
            .AddEventBroker(x => x.WithMaxConcurrentHandlers(maxConcurrentHandlers))
            .AddTransientEventHandler<TestEvent, TestEventHandler>()
            .AddSingleton(new HandlerSettings(RetryAttempts: 1, Delay: TimeSpan.FromMilliseconds(100)))
            .AddSingleton<EventsTracker>()
            .BuildServiceProvider(true);

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsTracker = scope.ServiceProvider.GetRequiredService<EventsTracker>();
        eventsTracker.ExpectedItemsCount = 2;
        var event1 = new TestEvent("test");

        // Act
        await eventBroker.Publish(event1);
        await eventsTracker.Wait(TimeSpan.FromSeconds(2));

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
        private readonly EventsTracker _tracker;
        private readonly HandlerSettings _settings;

        public TestEventHandler(HandlerSettings settings, EventsTracker tracker)
        {
            _settings = settings;
            _tracker = tracker;
        }

        public Task Handle(TestEvent @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken)
        {
            if(retryPolicy.Attempt < _settings.RetryAttempts)
            {
                retryPolicy.RetryAfter(_settings.Delay);
            }

            throw new NotImplementedException();
        }

        public Task OnError(Exception exception, TestEvent @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken)
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
