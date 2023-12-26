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

        var completed = await eventsRecorder.WaitForExpected();
        eventBroker.Shutdown();

        var exception = await Assert.ThrowsAsync<EventBrokerPublishNotAvailableException>(async () => await eventBroker.Publish(new TestEvent(CorrelationId: 2)));

        // Assert
        Assert.True(completed);
        Assert.Single(eventsRecorder.HandledEventIds);
        Assert.Equal(1, eventsRecorder.HandledEventIds[0]);
        Assert.Equal("EventBroker cannot publish event: Shutdown() or Dispose() has been called", exception.Message);
    }

    [Fact]
    public void Shutdown_ClosesChannel()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddEventBroker(_ => { });

        var channelKey = serviceCollection.Single(x => x.IsKeyedService && x.ServiceType == typeof(Channel<object>)).ServiceKey;

        var services = serviceCollection.BuildServiceProvider(true);

        var eventBroker = services.GetRequiredService<IEventBroker>();

        // Act
        eventBroker.Shutdown();

        // Assert
        var channel = services.GetRequiredKeyedService<Channel<object>>(channelKey);

        Assert.Throws<ChannelClosedException>(() => channel.Writer.Complete());
    }

    public record TestEvent(int CorrelationId) : ITraceable<int>;

    public class TestEventHandler : IEventHandler<TestEvent>
    {
        private readonly EventsRecorder<int> _eventsRecoder;

        public TestEventHandler(EventsRecorder<int> eventsRecorder)
        {
            _eventsRecoder = eventsRecorder;
        }

        public Task Handle(TestEvent @event)
        {
            _eventsRecoder.Notify(@event);
            return Task.CompletedTask;
        }

        public Task OnError(Exception exception, TestEvent @event)
        {
            _eventsRecoder.Notify(exception, @event);
            return Task.CompletedTask;
        }
    }
}
