﻿namespace M.EventBrokerSlim.Tests;

public class MultipleHandlersTests
{
    [Fact]
    public async Task MultipleHandlers_AllExecuted()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddEventBroker()
            .AddTransientEventHandler<TestEvent, TestEventHandler>()
            .AddTransientEventHandler<TestEvent, TestEventHandler1>()
            .AddTransientEventHandler<TestEvent, TestEventHandler2>()
            .AddSingleton<EventsRecorder<string>>()
            .BuildServiceProvider(true);

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsRecorder = scope.ServiceProvider.GetRequiredService<EventsRecorder<string>>();

        // Act
        var testEvent = new TestEvent(CorrelationId: "1");

        eventsRecorder.Expect(
            $"1_{typeof(TestEventHandler).Name}",
            $"1_{typeof(TestEventHandler1).Name}",
            $"1_{typeof(TestEventHandler2).Name}");

        await eventBroker.Publish(testEvent);

        var completed = await eventsRecorder.WaitForExpected(timeout: TimeSpan.FromSeconds(1));

        // Assert
        Assert.True(completed);
        Assert.Equal(3, eventsRecorder.HandledEventIds.Length);
        Assert.Contains($"1_{typeof(TestEventHandler).Name}", eventsRecorder.HandledEventIds);
        Assert.Contains($"1_{typeof(TestEventHandler1).Name}", eventsRecorder.HandledEventIds);
        Assert.Contains($"1_{typeof(TestEventHandler2).Name}", eventsRecorder.HandledEventIds);
    }

    public record TestEvent(string CorrelationId) : ITraceable<string>;

    public class TestEventHandler : IEventHandler<TestEvent>
    {
        private readonly EventsRecorder<string> _eventsRecorder;

        public TestEventHandler(EventsRecorder<string> eventsRecorder)
        {
            _eventsRecorder = eventsRecorder;
        }

        public Task Handle(TestEvent @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken)
        {
            _eventsRecorder.Notify($"{@event.CorrelationId}_{GetType().Name}");
            return Task.CompletedTask;
        }

        public Task OnError(Exception exception, TestEvent @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken)
        {
            _eventsRecorder.Notify(exception, @event);
            return Task.CompletedTask;
        }
    }

    public class TestEventHandler1 : TestEventHandler
    {
        public TestEventHandler1(EventsRecorder<string> eventsRecorder) : base(eventsRecorder)
        {
        }
    }

    public class TestEventHandler2 : TestEventHandler
    {
        public TestEventHandler2(EventsRecorder<string> eventsRecorder) : base(eventsRecorder)
        {
        }
    }
}
