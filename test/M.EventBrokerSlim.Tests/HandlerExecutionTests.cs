using System;
using System.Threading.Tasks;
using M.EventBrokerSlim.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace M.EventBrokerSlim.Tests;

public class HandlerExecutionTests
{
    [Fact]
    public async Task MaxConcurrentHandlers_IsOne_HandlersAreExecuted_Sequentially()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithEventsRecorder<int>(
            sc => sc.AddEventBroker(
                        x => x.AddKeyedTransient<TestEvent, TestEventHandler>()
                              .WithMaxConcurrentHandlers(1)));

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsRecorder = scope.ServiceProvider.GetRequiredService<EventsRecorder<int>>();

        // Act
        var event1 = new TestEvent(CorrelationId: 1, TimeToRun: TimeSpan.FromMilliseconds(50));
        var event2 = event1 with { CorrelationId = 2, TimeToRun = TimeSpan.FromMilliseconds(1) };
        eventsRecorder.Expect(event1, event2);

        await eventBroker.Publish(event1);
        await eventBroker.Publish(event2);

        var completed = await eventsRecorder.WaitForExpected(timeout: TimeSpan.FromMilliseconds(100));

        // Assert
        Assert.True(completed);
        Assert.Equal(2, eventsRecorder.HandledEventIds.Length);
        // second event completes faster, but will be executed after the first one is handled
        Assert.Equal(1, eventsRecorder.HandledEventIds[0]);
        Assert.Equal(2, eventsRecorder.HandledEventIds[1]);
    }

    [Fact]
    public async Task MaxConcurrentHandlers_IsGreaterThanOne_HandlersAreExecuted_InParallel()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithEventsRecorder<int>(
            sc => sc.AddEventBroker(
                        x => x.AddKeyedTransient<TestEvent, TestEventHandler>()
                              .WithMaxConcurrentHandlers(2)));

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsRecorder = scope.ServiceProvider.GetRequiredService<EventsRecorder<int>>();

        // Act
        var event1 = new TestEvent(CorrelationId: 1, TimeSpan.FromMilliseconds(50));
        var event2 = event1 with { CorrelationId = 2, TimeToRun = TimeSpan.FromMilliseconds(1) };
        eventsRecorder.Expect(event1, event2);

        await eventBroker.Publish(event1);
        await eventBroker.Publish(event2);

        var completed = await eventsRecorder.WaitForExpected(timeout: TimeSpan.FromMilliseconds(100));

        // Assert
        Assert.True(completed);
        Assert.Equal(2, eventsRecorder.HandledEventIds.Length);
        // second event is faster and will complete first
        Assert.Equal(2, eventsRecorder.HandledEventIds[0]);
        Assert.Equal(1, eventsRecorder.HandledEventIds[1]);
    }

    [Fact]
    public async Task NoHandlerRegistered_NothingHappens()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithEventsRecorder<int>(
            sc => sc.AddEventBroker(_ => { }));

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsRecorder = scope.ServiceProvider.GetRequiredService<EventsRecorder<int>>();

        // Act
        var event1 = new TestEvent(CorrelationId: 1);

        await eventBroker.Publish(event1);

        await eventsRecorder.Wait(timeout: TimeSpan.FromMilliseconds(50));

        // Assert
        Assert.Empty(eventsRecorder.HandledEventIds);
        Assert.Empty(eventsRecorder.Exceptions);
    }

    public record TestEvent(int CorrelationId, TimeSpan TimeToRun = default) : ITraceable<int>;

    public class TestEventHandler : IEventHandler<TestEvent>
    {
        private readonly EventsRecorder<int> _eventsRecoder;

        public TestEventHandler(EventsRecorder<int> eventsRecorder)
        {
            _eventsRecoder = eventsRecorder;
        }

        public async Task Handle(TestEvent @event)
        {
            if (@event.TimeToRun != default)
            {
                await Task.Delay(@event.TimeToRun);
            }

            _eventsRecoder.Notify(@event);
        }

        public Task OnError(Exception exception, TestEvent @event)
        {
            _eventsRecoder.Notify(exception, @event);
            return Task.CompletedTask;
        }
    }
}
