using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using M.EventBrokerSlim.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace M.EventBrokerSlim.Tests;

public class HandlerExecutionTests
{
    [Fact]
    public async Task MaxConcurrentHandlers_EqualsOne_HandlersAreExecuted_Sequentially()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddEventBroker(
            x => x.AddKeyedTransient<TestEvent, TestEventHandler>()
                  .AddKeyedSingleton<TestEventHandled, EventsRecorder>("orchestrator")
                  .WithMaxConcurrentHandlers(1));

        var services = serviceCollection.BuildServiceProvider(true);

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var orchestrator = (EventsRecorder)scope.ServiceProvider.GetRequiredKeyedService<IEventHandler<TestEventHandled>>("orchestrator");

        // Act
        var event1 = new TestEvent("Test Event", CorrelationId: 1, TimeToRun: TimeSpan.FromMilliseconds(50));
        var event2 = event1 with { CorrelationId = 2, TimeToRun = TimeSpan.FromMilliseconds(1) };
        orchestrator.Expect(event1, event2);

        await eventBroker.Publish(event1);
        await eventBroker.Publish(event2);

        var completed = await orchestrator.WaitForExpected(timeout: TimeSpan.FromMilliseconds(100));

        // Assert
        Assert.True(completed);
        Assert.Equal(2, orchestrator.HandledEventIds.Length);
        // second event is faster, but will be executed after the first one is handled
        Assert.Equal(1, orchestrator.HandledEventIds[0]);
        Assert.Equal(2, orchestrator.HandledEventIds[1]);
    }

    [Fact]
    public async Task MaxConcurrentHandlers_IsGreaterThanOne_HandlersAreExecuted_Sequentially()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddEventBroker(
            x => x.AddKeyedTransient<TestEvent, TestEventHandler>()
                  .AddKeyedSingleton<TestEventHandled, EventsRecorder>("orchestrator")
                  .WithMaxConcurrentHandlers(2));

        var services = serviceCollection.BuildServiceProvider(true);

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var orchestrator = (EventsRecorder)scope.ServiceProvider.GetRequiredKeyedService<IEventHandler<TestEventHandled>>("orchestrator");

        // Act
        var event1 = new TestEvent("Test Event", CorrelationId: 1, TimeSpan.FromMilliseconds(50));
        var event2 = event1 with { CorrelationId = 2, TimeToRun = TimeSpan.FromMilliseconds(1) };
        orchestrator.Expect(event1, event2);

        await eventBroker.Publish(event1);
        await eventBroker.Publish(event2);

        var completed = await orchestrator.WaitForExpected(timeout: TimeSpan.FromMilliseconds(100));

        // Assert
        Assert.True(completed);
        Assert.Equal(2, orchestrator.HandledEventIds.Length);
        // second event is faster and will complete first
        Assert.Equal(2, orchestrator.HandledEventIds[0]);
        Assert.Equal(1, orchestrator.HandledEventIds[1]);
    }

    [Fact]
    public async Task NoHandlerRegistered_NothingHappens()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddEventBroker(
            x => x.AddKeyedSingleton<TestEventHandled, EventsRecorder>("orchestrator"));

        var services = serviceCollection.BuildServiceProvider(true);

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var orchestrator = (EventsRecorder)scope.ServiceProvider.GetRequiredKeyedService<IEventHandler<TestEventHandled>>("orchestrator");

        // Act
        var event1 = new TestEvent("Test Event", CorrelationId: 1);

        await eventBroker.Publish(event1);

        await orchestrator.Wait(timeout: TimeSpan.FromMilliseconds(50));

        // Assert
        Assert.Empty(orchestrator.HandledEventIds);
        Assert.Empty(orchestrator.Exceptions);
    }

    [Fact]
    public async Task Exception_WhenResolvingHandler_IsHandled()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddScoped(_ => "scoped");
        // try to inject scoped instance into singleton -> runtime error
        serviceCollection.AddEventBroker(
            x => x.AddKeyedSingleton<TestEvent, TestEventHandler1>()
                  .AddKeyedSingleton<TestEventHandled, EventsRecorder>("orchestrator"));

        var services = serviceCollection.BuildServiceProvider(true);

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var orchestrator = (EventsRecorder)scope.ServiceProvider.GetRequiredKeyedService<IEventHandler<TestEventHandled>>("orchestrator");

        // Act
        var event1 = new TestEvent("Test Event", CorrelationId: 1);

        await eventBroker.Publish(event1);

        await orchestrator.Wait(timeout: TimeSpan.FromMilliseconds(50));

        // Assert
        Assert.Empty(orchestrator.HandledEventIds);
        Assert.Empty(orchestrator.Exceptions);
    }

    public record TestEvent(string Message, int CorrelationId, TimeSpan TimeToRun = default) : ITraceable<int>;

    public record TestEventHandled(int CorrelationId) : ITraceable<int>;

    public class EventsRecorder : Orchestrator<int, TestEventHandled>
    {
        private readonly ConcurrentBag<(int id, long tick)> _events = new();

        public override async Task Handle(TestEventHandled @event)
        {
            await base.Handle(@event);
            _events.Add((@event.CorrelationId, DateTime.UtcNow.Ticks));
        }

        public int[] HandledEventIds => _events.OrderBy(x => x.tick).Select(x => x.id).ToArray();
    }

    public class TestEventHandler : IEventHandler<TestEvent>
    {
        private readonly IEventBroker _eventBroker;

        public TestEventHandler(IEventBroker eventBroker)
        {
            _eventBroker = eventBroker;
        }

        public async Task Handle(TestEvent @event)
        {
            if (@event.TimeToRun != default)
            {
                await Task.Delay(@event.TimeToRun);
            }

            var handled = new TestEventHandled(CorrelationId: @event.CorrelationId);

            await _eventBroker.Publish(handled);
        }

        public Task OnError(Exception exception, TestEvent @event)
        {
            throw new NotImplementedException();
        }
    }

    public class TestEventHandler1 : IEventHandler<TestEvent>
    {
        private readonly string _input;

        public TestEventHandler1(string input)
        {
            _input = input;
        }

        public Task Handle(TestEvent @event) => throw new NotImplementedException();

        public Task OnError(Exception exception, TestEvent @event) => throw new NotImplementedException();
    }
}
