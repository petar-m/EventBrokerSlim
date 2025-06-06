﻿namespace M.EventBrokerSlim.Tests;

public class RetryPolicyTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task MultipleRetries_RetryPolicy_IsTheSameInstance_EveryTime(int maxConcurrentHandlers)
    {
        // Arrange
        var services = new ServiceCollection()
            .AddEventBroker(x => x.WithMaxConcurrentHandlers(maxConcurrentHandlers))
            .AddTransientEventHandler<TestEvent, TestEventHandler>()
            .AddSingleton(new HandlerSettings(RetryAttempts: 3, Delay: TimeSpan.FromMilliseconds(100)))
            .AddSingleton<EventsTracker>()
            .BuildServiceProvider(true);

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsTracker = scope.ServiceProvider.GetRequiredService<EventsTracker>();
        var event1 = new TestEvent("test");

        // Act
        await eventBroker.Publish(event1);
        await eventsTracker.Wait(TimeSpan.FromSeconds(1));

        // Assert
        Assert.Equal(8, eventsTracker.Items.Count);
        var retryPolicy = eventsTracker.Items.First().Item;
        Assert.All(eventsTracker.Items.Select(x => x.Item), x => Assert.Same(retryPolicy, x));
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
            _tracker.Track(retryPolicy);
            if(retryPolicy.Attempt < _settings.RetryAttempts)
            {
                retryPolicy.RetryAfter(_settings.Delay);
            }

            throw new NotImplementedException();
        }

        public Task OnError(Exception exception, TestEvent @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken)
        {
            _tracker.Track(retryPolicy);
            if(retryPolicy.Attempt < _settings.RetryAttempts)
            {
                retryPolicy.RetryAfter(_settings.Delay);
            }

            return Task.CompletedTask;
        }
    }
}
