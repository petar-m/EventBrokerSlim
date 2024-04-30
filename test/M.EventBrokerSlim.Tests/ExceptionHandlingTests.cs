﻿using MELT;
using Microsoft.Extensions.Logging;

namespace M.EventBrokerSlim.Tests;
public class ExceptionHandlingTests
{
    [Fact]
    public async Task UnhandledException_FromEventHandler_IsPassedTo_OnError()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithEventsRecorder<int>(
            sc => sc.AddEventBroker(
                        x => x.AddKeyedTransient<TestEvent, TestEventHandler>()));

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsRecorder = scope.ServiceProvider.GetRequiredService<EventsRecorder<int>>();

        // Act
        var testEvent = new TestEvent(CorrelationId: 1, ThrowFromHandle: true);

        await eventBroker.Publish(testEvent);

        await eventsRecorder.Wait(timeout: TimeSpan.FromMilliseconds(50));

        // Assert
        Assert.Single(eventsRecorder.Exceptions);
        Assert.IsType<NotImplementedException>(eventsRecorder.Exceptions[0]);
    }

    [Fact]
    public async Task Exception_WhenResolvingHandler_IsHandled()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithEventsRecorder<int>(
            sc => sc.AddEventBroker(
                        // TestEventHandler1 has dependency on string not configured in the DI container 
                        x => x.AddKeyedSingleton<TestEvent, TestEventHandler1>()));

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsRecorder = scope.ServiceProvider.GetRequiredService<EventsRecorder<int>>();

        // Act
        var testEvent = new TestEvent(CorrelationId: 1);

        await eventBroker.Publish(testEvent);

        await eventsRecorder.Wait(timeout: TimeSpan.FromMilliseconds(50));

        // Assert
        Assert.Empty(eventsRecorder.HandledEventIds);
        Assert.Empty(eventsRecorder.Exceptions);
    }

    [Fact]
    public async Task Exception_WhenResolvingHandler_Is_Logged()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithEventsRecorderAndLogger<int>(
            sc => sc.AddEventBroker(
                        // TestEventHandler1 has dependency on string not configured in the DI container 
                        x => x.AddKeyedSingleton<TestEvent, TestEventHandler1>()));

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsRecorder = scope.ServiceProvider.GetRequiredService<EventsRecorder<int>>();

        // Act
        var testEvent = new TestEvent(CorrelationId: 1);

        await eventBroker.Publish(testEvent);

        await eventsRecorder.Wait(timeout: TimeSpan.FromMilliseconds(50));

        // Assert
        var provider = (TestLoggerProvider)scope.ServiceProvider.GetServices<ILoggerProvider>().Single(x => x is TestLoggerProvider);

        var log = Assert.Single(provider.Sink.LogEntries);
        Assert.Equal(LogLevel.Error, log.LogLevel);
        Assert.Equal("Can't resolve event handler for event M.EventBrokerSlim.Tests.ExceptionHandlingTests+TestEvent", log.Message);
    }

    [Fact]
    public async Task UnhandledException_FromOnError_IsSuppressed()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithEventsRecorder<int>(
            sc => sc.AddEventBroker(
                        x => x.AddKeyedTransient<TestEvent, TestEventHandler>()));

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsRecorder = scope.ServiceProvider.GetRequiredService<EventsRecorder<int>>();

        // Act
        var event1 = new TestEvent(CorrelationId: 1, ThrowFromHandle: true, ThrowFromOnError: true);

        await eventBroker.Publish(event1);

        await eventsRecorder.Wait(timeout: TimeSpan.FromMilliseconds(50));

        // Assert
        Assert.Single(eventsRecorder.Exceptions);
        Assert.IsType<NotImplementedException>(eventsRecorder.Exceptions[0]);
    }

    [Fact]
    public async Task UnhandledException_FromOnError_IsLogged()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithEventsRecorderAndLogger<int>(
            sc => sc.AddEventBroker(
                        x => x.AddKeyedTransient<TestEvent, TestEventHandler>()));

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsRecorder = scope.ServiceProvider.GetRequiredService<EventsRecorder<int>>();

        // Act
        var event1 = new TestEvent(CorrelationId: 1, ThrowFromHandle: true, ThrowFromOnError: true);

        await eventBroker.Publish(event1);

        await eventsRecorder.Wait(timeout: TimeSpan.FromMilliseconds(50));

        // Assert
        var provider = (TestLoggerProvider)scope.ServiceProvider.GetServices<ILoggerProvider>().Single(x => x is TestLoggerProvider);

        var log = Assert.Single(provider.Sink.LogEntries);
        Assert.Equal(LogLevel.Error, log.LogLevel);
        Assert.Equal("Unhandled exception executing M.EventBrokerSlim.Tests.ExceptionHandlingTests+TestEventHandler.OnError()", log.Message);
    }

    public record TestEvent(int CorrelationId, bool ThrowFromHandle = false, bool ThrowFromOnError = false) : ITraceable<int>;

    public class TestEventHandler : IEventHandler<TestEvent>
    {
        private readonly EventsRecorder<int> _eventsRecorder;

        public TestEventHandler(EventsRecorder<int> eventBroker)
        {
            _eventsRecorder = eventBroker;
        }

        public Task Handle(TestEvent @event, RetryPolicy retryPolicy, CancellationToken cancellationToken)
        {
            _eventsRecorder.Notify(@event);
            if (@event.ThrowFromHandle)
            {
                throw new NotImplementedException();
            }

            return Task.CompletedTask;
        }

        public Task OnError(Exception exception, TestEvent @event, RetryPolicy retryPolicy, CancellationToken cancellationToken)
        {
            _eventsRecorder.Notify(exception, @event);
            if (@event.ThrowFromOnError)
            {
                throw new NotImplementedException();
            }
            return Task.CompletedTask;
        }
    }

    public class TestEventHandler1 : IEventHandler<TestEvent>
    {
        private readonly string _input;

        public TestEventHandler1(string input)
        {
            _input = input;
        }

        public Task Handle(TestEvent @even, RetryPolicy retryPolicyt, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task OnError(Exception exception, TestEvent @event, RetryPolicy retryPolicy, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
