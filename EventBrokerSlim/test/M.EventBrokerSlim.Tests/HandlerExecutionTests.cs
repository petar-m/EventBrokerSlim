using MELT;
using Microsoft.Extensions.Logging;

namespace M.EventBrokerSlim.Tests;

public class HandlerExecutionTests
{
    [Fact]
    public async Task MaxConcurrentHandlers_IsOne_HandlersAreExecuted_Sequentially()
    {
        // Arrange
        using var services = new ServiceCollection()
            .AddEventBroker(x => x.WithMaxConcurrentHandlers(1))
            .AddTransientEventHandler<TestEvent, TestEventHandler>()
            .AddSingleton<EventsRecorder<int>>()
            .BuildServiceProvider(true);
        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsRecorder = scope.ServiceProvider.GetRequiredService<EventsRecorder<int>>();

        // Act
        var event1 = new TestEvent(CorrelationId: 1, TimeToRun: TimeSpan.FromMilliseconds(50));
        var event2 = event1 with { CorrelationId = 2, TimeToRun = TimeSpan.FromMilliseconds(1) };
        eventsRecorder.Expect(event1, event2);

        await eventBroker.Publish(event1);
        await eventBroker.Publish(event2);

        var completed = await eventsRecorder.WaitForExpected(timeout: TimeSpan.FromSeconds(1));

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
        using var services = new ServiceCollection()
            .AddEventBroker(x => x.WithMaxConcurrentHandlers(2))
            .AddTransientEventHandler<TestEvent, TestEventHandler>()
            .AddSingleton<EventsRecorder<int>>()
            .BuildServiceProvider(true);
        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsRecorder = scope.ServiceProvider.GetRequiredService<EventsRecorder<int>>();

        // Act
        var event1 = new TestEvent(CorrelationId: 1, TimeSpan.FromMilliseconds(50));
        var event2 = event1 with { CorrelationId = 2, TimeToRun = TimeSpan.FromMilliseconds(1) };
        eventsRecorder.Expect(event1, event2);

        await eventBroker.Publish(event1);
        await eventBroker.Publish(event2);

        var completed = await eventsRecorder.WaitForExpected(timeout: TimeSpan.FromSeconds(1));

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
        using var services = new ServiceCollection()
            .AddEventBroker()
            .AddSingleton<EventsRecorder<int>>()
            .BuildServiceProvider(true);
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
        using var services = new ServiceCollection()
            .AddEventBroker()
            .AddLogging(x => x.AddTest())
            .AddSingleton<EventsRecorder<int>>()
            .BuildServiceProvider(true);
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
        using var services = new ServiceCollection()
            .AddEventBroker(x => x.DisableMissingHandlerWarningLog())
            .AddLogging(x => x.AddTest())
            .AddSingleton<EventsRecorder<int>>()
            .BuildServiceProvider(true);
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
        private readonly EventsRecorder<int> _eventsRecorder;

        public TestEventHandler(EventsRecorder<int> eventsRecorder)
        {
            _eventsRecorder = eventsRecorder;
        }

        public async Task Handle(TestEvent @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken)
        {
            if(@event.TimeToRun != default)
            {
                await Task.Delay(@event.TimeToRun, cancellationToken);
            }

            _eventsRecorder.Notify(@event);
        }

        public Task OnError(Exception exception, TestEvent @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken)
        {
            _eventsRecorder.Notify(exception, @event);
            return Task.CompletedTask;
        }
    }
}
