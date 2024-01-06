using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using M.EventBrokerSlim.DependencyInjection;
using MELT;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
                        x => x.WithMaxConcurrentHandlers(1)
                              .AddKeyedTransient<TestEvent, TestEventHandler>()));

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
                        x => x.WithMaxConcurrentHandlers(2)
                              .AddKeyedTransient<TestEvent, TestEventHandler>()));

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
    public async Task NoHandlerRegistered_NoLogger_NothingHappens()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithEventsRecorder<int>(
            sc => sc.AddEventBroker());

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

    [Fact]
    public async Task NoHandlerRegistered_LogsWarning_IfEnabled()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithEventsRecorderAndLogger<int>(
            sc => sc.AddEventBroker());

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsRecorder = scope.ServiceProvider.GetRequiredService<EventsRecorder<int>>();

        // Act
        var event1 = new TestEvent(CorrelationId: 1);

        await eventBroker.Publish(event1);

        await eventsRecorder.Wait(timeout: TimeSpan.FromMilliseconds(50));

        // Assert
        var provider = (TestLoggerProvider)scope.ServiceProvider.GetServices<ILoggerProvider>().Single(x => x is TestLoggerProvider);

        var log = Assert.Single(provider.Sink.LogEntries);
        Assert.Equal(LogLevel.Warning, log.LogLevel);
        Assert.Equal("No event handler found for event M.EventBrokerSlim.Tests.HandlerExecutionTests+TestEvent", log.Message);
    }

    [Fact]
    public async Task NoHandlerRegistered_LogsWarning_IfDisabled()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithEventsRecorderAndLogger<int>(
            sc => sc.AddEventBroker(x => x.DisableMissingHandlerWarningLog()));

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsRecorder = scope.ServiceProvider.GetRequiredService<EventsRecorder<int>>();

        // Act
        var event1 = new TestEvent(CorrelationId: 1);

        await eventBroker.Publish(event1);

        await eventsRecorder.Wait(timeout: TimeSpan.FromMilliseconds(50));

        // Assert
        var provider = (TestLoggerProvider)scope.ServiceProvider.GetServices<ILoggerProvider>().Single(x => x is TestLoggerProvider);

        Assert.Empty(provider.Sink.LogEntries);
    }

    public record TestEvent(int CorrelationId, TimeSpan TimeToRun = default) : ITraceable<int>;

    public class TestEventHandler : IEventHandler<TestEvent>
    {
        private readonly EventsRecorder<int> _eventsRecoder;

        public TestEventHandler(EventsRecorder<int> eventsRecorder)
        {
            _eventsRecoder = eventsRecorder;
        }

        public async Task Handle(TestEvent @event, CancellationToken cancellationToken)
        {
            if (@event.TimeToRun != default)
            {
                await Task.Delay(@event.TimeToRun);
            }

            _eventsRecoder.Notify(@event);
        }

        public Task OnError(Exception exception, TestEvent @event, CancellationToken cancellationToken)
        {
            _eventsRecoder.Notify(exception, @event);
            return Task.CompletedTask;
        }
    }
}
