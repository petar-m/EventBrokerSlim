using System.Threading.Channels;
using MELT;
using Microsoft.Extensions.Logging;

namespace M.EventBrokerSlim.Tests;

public class EventBrokerTests
{
    [Fact]
    public async Task Publish_Null_Throws()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithEventsRecorder<int>(
            sc => sc.AddEventBroker(
                        x => x.AddTransient<TestEvent, TestEventHandler>()));
        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();

        // Act Assert
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        await Assert.ThrowsAsync<ArgumentNullException>("event", async () => await eventBroker.Publish<TestEvent>(null));
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }

    [Fact]
    public async Task PublishDeferred_Event_Is_Null_Throws()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithEventsRecorder<int>(
            sc => sc.AddEventBroker(
                        x => x.AddTransient<TestEvent, TestEventHandler>()));
        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();

        // Act Assert
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        await Assert.ThrowsAsync<ArgumentNullException>("event", async () => await eventBroker.PublishDeferred<TestEvent>(null, TimeSpan.FromSeconds(1)));
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }

    [Fact]
    public async Task PublishDeferred_DeferDuration_Is_LessToZero_Throws()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithEventsRecorder<int>(
            sc => sc.AddEventBroker(
                        x => x.AddTransient<TestEvent, TestEventHandler>()));
        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();

        // Act Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>("deferDuration",
            async () => await eventBroker.PublishDeferred(new TestEvent(1), TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public async Task PublishDeferred_DeferDuration_Is_Zero_Throws()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithEventsRecorder<int>(
            sc => sc.AddEventBroker(
                        x => x.AddTransient<TestEvent, TestEventHandler>()));
        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();

        // Act Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>("deferDuration",
            async () => await eventBroker.PublishDeferred(new TestEvent(1), TimeSpan.FromSeconds(0)));
    }

    [Fact]
    public void Shutdown_CanBeCalledMultipleTimes()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithEventsRecorder<int>(
            sc => sc.AddEventBroker(
                        x => x.AddTransient<TestEvent, TestEventHandler>()));
        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();

        // Act
        var exception = Record.Exception(() =>
        {
            eventBroker.Shutdown();
            eventBroker.Shutdown();
        });

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task Publish_AfterShutdown_Throws()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithEventsRecorder<int>(
            sc => sc.AddEventBroker(
                        x => x.AddTransient<TestEvent, TestEventHandler>()));
        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsRecorder = scope.ServiceProvider.GetRequiredService<EventsRecorder<int>>();

        // Act
        eventsRecorder.Expect(1);
        await eventBroker.Publish(new TestEvent(CorrelationId: 1));

        var completed = await eventsRecorder.WaitForExpected(TimeSpan.FromMilliseconds(50));
        eventBroker.Shutdown();

        var exception = await Assert.ThrowsAsync<EventBrokerPublishNotAvailableException>(async () => await eventBroker.Publish(new TestEvent(CorrelationId: 2)));

        // Assert
        Assert.True(completed);
        Assert.Single(eventsRecorder.HandledEventIds);
        Assert.Equal(1, eventsRecorder.HandledEventIds[0]);
        Assert.Equal("EventBroker cannot publish event: Shutdown() has been called", exception.Message);
    }

    [Fact]
    public async Task PublishDeferred_AfterShutdown_DoesNotThrow()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithEventsRecorder<int>(
            sc => sc.AddEventBroker(
                        x => x.AddTransient<TestEvent, TestEventHandler>()));
        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsRecorder = scope.ServiceProvider.GetRequiredService<EventsRecorder<int>>();

        // Act
        eventsRecorder.Expect(1);

        eventBroker.Shutdown();
        await eventBroker.PublishDeferred(new TestEvent(CorrelationId: 1), TimeSpan.FromMilliseconds(20));

        var completed = await eventsRecorder.WaitForExpected(TimeSpan.FromMilliseconds(100));

        // Assert
        Assert.False(completed);
        Assert.Empty(eventsRecorder.HandledEventIds);
    }

    [Fact]
    public void Shutdown_ClosesChannel()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddEventBroker();

        var channelKey = serviceCollection.Single(x => x.IsKeyedService && x.ServiceType == typeof(Channel<object>)).ServiceKey;

        var services = serviceCollection.BuildServiceProvider(true);

        var eventBroker = services.GetRequiredService<IEventBroker>();

        // Act
        eventBroker.Shutdown();

        // Assert
        var channel = services.GetRequiredKeyedService<Channel<object>>(channelKey);

        Assert.Throws<ChannelClosedException>(() => channel.Writer.Complete());
    }

    [Fact]
    public async Task PublishDeferred_ExecutesHandler_After_DeferredDuration()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithEventsRecorder<int>(
            sc => sc.AddEventBroker(
                        x => x.AddTransient<TestEvent, TestEventHandler>())
                    .AddSingleton<Timestamp>());

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsRecorder = scope.ServiceProvider.GetRequiredService<EventsRecorder<int>>();

        // Act
        eventsRecorder.Expect(1);
        var calledPublishDeferredAt = DateTime.UtcNow;

        await eventBroker.PublishDeferred(new TestEvent(CorrelationId: 1), TimeSpan.FromMilliseconds(200));

        var completed = await eventsRecorder.WaitForExpected(TimeSpan.FromMilliseconds(300));

        // Assert
        Assert.True(completed);
        Assert.Single(eventsRecorder.HandledEventIds);
        Assert.Equal(1, eventsRecorder.HandledEventIds[0]);

        var handlerExecutedAt = scope.ServiceProvider.GetRequiredService<Timestamp>().ExecutedAt;
        Assert.True(handlerExecutedAt - calledPublishDeferredAt >= TimeSpan.FromMilliseconds(200));
    }

    [Fact]
    public async Task PublishDeferred_DoesNotBlock_Publish()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithEventsRecorder<int>(
            sc => sc.AddEventBroker(
                        x => x.AddTransient<TestEvent, TestEventHandler>()));
        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsRecorder = scope.ServiceProvider.GetRequiredService<EventsRecorder<int>>();

        // Act
        eventsRecorder.Expect(1, 2);
        await eventBroker.PublishDeferred(new TestEvent(CorrelationId: 1), TimeSpan.FromMilliseconds(300));
        await eventBroker.Publish(new TestEvent(CorrelationId: 2));

        var completed = await eventsRecorder.WaitForExpected(TimeSpan.FromMilliseconds(400));

        // Assert
        Assert.True(completed);
        Assert.Collection(eventsRecorder.HandledEventIds,
            x => Assert.Equal(2, x),
            x => Assert.Equal(1, x));
    }

    [Fact]
    public async Task PublishDeferred_DelayedTasks_Cancelled_OnShutdown()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithEventsRecorder<int>(
            sc => sc.AddEventBroker(
                        x => x.AddTransient<TestEvent, TestEventHandler>())
                    .AddSingleton<Timestamp>());

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsRecorder = scope.ServiceProvider.GetRequiredService<EventsRecorder<int>>();

        var expected = Enumerable.Range(1, 10).Select(x => new TestEvent(x)).ToArray();
        eventsRecorder.Expect(expected);

        // Act
        foreach(var @event in expected)
        {
            await eventBroker.PublishDeferred(@event, TimeSpan.FromMilliseconds(200));
        }

        eventBroker.Shutdown();

        var completed = await eventsRecorder.WaitForExpected(TimeSpan.FromMilliseconds(300));

        // Assert
        Assert.False(completed);
        Assert.Empty(eventsRecorder.HandledEventIds);
    }

    [Fact]
    public async Task Shutdown_WhileHandlingEvent_TaskCancelledException_HandledByOnError()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithEventsRecorder<int>(
            sc => sc.AddEventBroker(
                        x => x.WithMaxConcurrentHandlers(2)
                              .AddTransient<TestEvent, TestEventHandler>())
                    .AddSingleton<Timestamp>());

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsRecorder = scope.ServiceProvider.GetRequiredService<EventsRecorder<int>>();

        var expected = Enumerable.Range(1, 10).Select(x => new TestEvent(x, HandlingDuration: TimeSpan.FromMilliseconds(500))).ToArray();
        eventsRecorder.Expect(expected);

        // Act
        foreach(var @event in expected)
        {
            await eventBroker.Publish(@event);
        }

        await Task.Delay(TimeSpan.FromMilliseconds(200));

        eventBroker.Shutdown();

        var completed = await eventsRecorder.WaitForExpected(TimeSpan.FromSeconds(1));

        // Assert
        Assert.False(completed);
        Assert.Collection(eventsRecorder.HandledEventIds.Order(),
            x => Assert.Equal(1, x),
            x => Assert.Equal(2, x));

        Assert.Collection(eventsRecorder.Exceptions,
            x => Assert.IsType<TaskCanceledException>(x),
            x => Assert.IsType<TaskCanceledException>(x));
    }

    [Fact]
    public async Task Shutdown_PendingEvents_AreNot_Processed()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithEventsRecorder<int>(
            sc => sc.AddEventBroker(
                        x => x.WithMaxConcurrentHandlers(2)
                              .AddTransient<TestEvent, TestEventHandler>())
                    .AddSingleton<Timestamp>());

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsRecorder = scope.ServiceProvider.GetRequiredService<EventsRecorder<int>>();

        var expected = Enumerable.Range(1, 10).Select(x => new TestEvent(x, HandlingDuration: TimeSpan.FromMilliseconds(200))).ToArray();
        eventsRecorder.Expect(expected);

        // Act
        foreach(var @event in expected)
        {
            await eventBroker.Publish(@event);
        }

        await Task.Delay(TimeSpan.FromMilliseconds(100));

        eventBroker.Shutdown();

        var completed = await eventsRecorder.WaitForExpected(TimeSpan.FromMilliseconds(300));

        // Assert
        Assert.False(completed);

        Assert.Equal(8, eventsRecorder.Expected.Length);

        Assert.DoesNotContain(1, eventsRecorder.Expected);
        Assert.DoesNotContain(2, eventsRecorder.Expected);
    }

    [Fact]
    public async Task Shutdown_WhileHandlingError_TaskCancelledException_IsLogged()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithEventsRecorderAndLogger<int>(
            sc => sc.AddEventBroker(
                        x => x.WithMaxConcurrentHandlers(2)
                              .AddTransient<TestEvent, TestEventHandler>())
                    .AddSingleton<Timestamp>());

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsRecorder = scope.ServiceProvider.GetRequiredService<EventsRecorder<int>>();

        var testEvent = new TestEvent(CorrelationId: 1, ThrowFromHandle: true, ErrorHandlingDuration: TimeSpan.FromMilliseconds(300));
        eventsRecorder.Expect(testEvent);

        // Act
        await eventBroker.Publish(testEvent);

        await Task.Delay(TimeSpan.FromMilliseconds(100));

        eventBroker.Shutdown();

        await eventsRecorder.Wait(TimeSpan.FromMilliseconds(100));

        // Assert

        var provider = (TestLoggerProvider)scope.ServiceProvider.GetServices<ILoggerProvider>().Single(x => x is TestLoggerProvider);

        var log = Assert.Single(provider.Sink.LogEntries);
        Assert.Equal(LogLevel.Error, log.LogLevel);
        Assert.Equal("Unhandled exception executing M.EventBrokerSlim.Tests.EventBrokerTests+TestEventHandler.OnError()", log.Message);
        Assert.IsType<TaskCanceledException>(log.Exception);
    }

    public record TestEvent(
        int CorrelationId,
        TimeSpan HandlingDuration = default,
        bool ThrowFromHandle = false,
        TimeSpan ErrorHandlingDuration = default) : ITraceable<int>;

    public class TestEventHandler : IEventHandler<TestEvent>
    {
        private readonly EventsRecorder<int> _eventsRecorder;
        private readonly Timestamp? _timestamp;

        public TestEventHandler(EventsRecorder<int> eventsRecorder, Timestamp? timestamp = null)
        {
            _eventsRecorder = eventsRecorder;
            _timestamp = timestamp;
        }

        public async Task Handle(TestEvent @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken)
        {
            _eventsRecorder.Notify(@event);

            if(_timestamp is not null)
            {
                _timestamp.ExecutedAt = DateTime.UtcNow;
            }

            if(@event.ThrowFromHandle)
            {
                throw new InvalidOperationException("Exception during event handling");
            }

            if(@event.HandlingDuration != default)
            {
                await Task.Delay(@event.HandlingDuration, cancellationToken);
            }
        }

        public async Task OnError(Exception exception, TestEvent @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken)
        {
            _eventsRecorder.Notify(exception, @event);

            if(@event.ErrorHandlingDuration != default)
            {
                await Task.Delay(@event.ErrorHandlingDuration, cancellationToken);
            }
        }
    }

    public class Timestamp
    {
        public DateTime ExecutedAt { get; set; }
    }
}
