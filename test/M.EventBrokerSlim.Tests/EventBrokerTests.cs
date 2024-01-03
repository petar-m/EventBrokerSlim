using System;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;
using M.EventBrokerSlim.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace M.EventBrokerSlim.Tests;

public class EventBrokerTests
{
    [Fact]
    public async Task Publish_Null_Throws()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithEventsRecorder<int>(
            sc => sc.AddEventBroker(
                        x => x.AddKeyedTransient<TestEvent, TestEventHandler>()));
        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();

        // Act Assert
        await Assert.ThrowsAsync<ArgumentNullException>("event", async () => await eventBroker.Publish<TestEvent>(null));
    }

    [Fact]
    public async Task PublishDeferred_Event_Is_Null_Throws()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithEventsRecorder<int>(
            sc => sc.AddEventBroker(
                        x => x.AddKeyedTransient<TestEvent, TestEventHandler>()));
        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();

        // Act Assert
        await Assert.ThrowsAsync<ArgumentNullException>("event", async () => await eventBroker.PublishDeferred<TestEvent>(null, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task PublishDeferred_DeferDuration_Is_LessToZero_Throws()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithEventsRecorder<int>(
            sc => sc.AddEventBroker(
                        x => x.AddKeyedTransient<TestEvent, TestEventHandler>()));
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
                        x => x.AddKeyedTransient<TestEvent, TestEventHandler>()));
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
                        x => x.AddKeyedTransient<TestEvent, TestEventHandler>()));
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
    public async Task Publish_AfterShoutdown_Throws()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithEventsRecorder<int>(
            sc => sc.AddEventBroker(
                        x => x.AddKeyedTransient<TestEvent, TestEventHandler>()));
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
    public async Task PublishDeferred_AfterShoutdown_DoesNotThrow()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithEventsRecorder<int>(
            sc => sc.AddEventBroker(
                        x => x.AddKeyedTransient<TestEvent, TestEventHandler>()));
        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsRecorder = scope.ServiceProvider.GetRequiredService<EventsRecorder<int>>();

        // Act
        eventsRecorder.Expect(1);

        eventBroker.Shutdown();
        await eventBroker.PublishDeferred(new TestEvent(CorrelationId: 1), TimeSpan.FromMicroseconds(20));

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
                        x => x.AddKeyedTransient<TestEvent, TestEventHandler>())
                    .AddSingleton<Timestamp>());

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsRecorder = scope.ServiceProvider.GetRequiredService<EventsRecorder<int>>();

        // Act
        eventsRecorder.Expect(1);
        var calledPublisheDeferredAt = DateTime.UtcNow;

        await eventBroker.PublishDeferred(new TestEvent(CorrelationId: 1), TimeSpan.FromSeconds(1));

        var completed = await eventsRecorder.WaitForExpected(TimeSpan.FromSeconds(2));

        // Assert
        Assert.True(completed);
        Assert.Single(eventsRecorder.HandledEventIds);
        Assert.Equal(1, eventsRecorder.HandledEventIds[0]);

        var handlerExecutedAt = scope.ServiceProvider.GetRequiredService<Timestamp>().ExecutedAt;
        Assert.True(handlerExecutedAt - calledPublisheDeferredAt >= TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task PublishDeferred_DoesNotBlock_Publish()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithEventsRecorder<int>(
            sc => sc.AddEventBroker(
                        x => x.AddKeyedTransient<TestEvent, TestEventHandler>()));
        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsRecorder = scope.ServiceProvider.GetRequiredService<EventsRecorder<int>>();

        // Act
        eventsRecorder.Expect(1, 2);
        await eventBroker.PublishDeferred(new TestEvent(CorrelationId: 1), TimeSpan.FromMilliseconds(300));
        await eventBroker.Publish(new TestEvent(CorrelationId: 2));

        var completed = await eventsRecorder.WaitForExpected(TimeSpan.FromMilliseconds(350));

        // Assert
        Assert.True(completed);
        Assert.Collection(eventsRecorder.HandledEventIds,
            x => Assert.Equal(2, x),
            x => Assert.Equal(1, x));
    }

    public record TestEvent(int CorrelationId) : ITraceable<int>;

    public class TestEventHandler : IEventHandler<TestEvent>
    {
        private readonly EventsRecorder<int> _eventsRecoder;
        private readonly Timestamp _timestamp;

        public TestEventHandler(EventsRecorder<int> eventsRecorder, Timestamp timestamp = null)
        {
            _eventsRecoder = eventsRecorder;
            _timestamp = timestamp;
        }

        public Task Handle(TestEvent @event)
        {
            if (_timestamp is not null)
            {
                _timestamp.ExecutedAt = DateTime.UtcNow;
            }

            _eventsRecoder.Notify(@event);
            return Task.CompletedTask;
        }

        public Task OnError(Exception exception, TestEvent @event)
        {
            _eventsRecoder.Notify(exception, @event);
            return Task.CompletedTask;
        }
    }

    public class Timestamp
    {
        public DateTime ExecutedAt { get; set; }
    }
}
