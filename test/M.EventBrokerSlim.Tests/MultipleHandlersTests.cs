using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using M.EventBrokerSlim.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace M.EventBrokerSlim.Tests;

public class MultipleHandlersTests
{
    [Fact]
    public async Task MultipleHandlers_AllExecuted()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddEventBroker(
            x => x.AddKeyedTransient<TestEvent, TestEventHandler>()
                  .AddKeyedTransient<TestEvent, TestEventHandler1>()
                  .AddKeyedTransient<TestEvent, TestEventHandler2>()
                  .AddKeyedSingleton<TestEventHandled, EventsRecorder>("orchestrator")
                  .WithMaxConcurrentHandlers(2));

        var services = serviceCollection.BuildServiceProvider(true);

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var orchestrator = (EventsRecorder)scope.ServiceProvider.GetRequiredKeyedService<IEventHandler<TestEventHandled>>("orchestrator");

        // Act
        var event1 = new TestEvent("Test Event", CorrelationId: "1");
        
        orchestrator.Expect(
            $"1_{typeof(TestEventHandler).Name}",
            $"1_{typeof(TestEventHandler1).Name}",
            $"1_{typeof(TestEventHandler2).Name}");

        await eventBroker.Publish(event1);

        var completed = await orchestrator.WaitForExpected(timeout: TimeSpan.FromMilliseconds(50));

        // Assert
        Assert.True(completed);
        Assert.Equal(3, orchestrator.HandledEventIds.Length);
        Assert.Contains($"1_{typeof(TestEventHandler).Name}", orchestrator.HandledEventIds);
        Assert.Contains($"1_{typeof(TestEventHandler1).Name}", orchestrator.HandledEventIds);
        Assert.Contains($"1_{typeof(TestEventHandler2).Name}", orchestrator.HandledEventIds);
    }

    [Fact]
    public async Task MultipleKeyedHandlers_AllExecuted()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddEventBroker(
            x => x.AddKeyedTransient<TestEvent, TestEventHandler>("1")
                  .AddKeyedTransient<TestEvent, TestEventHandler1>("2")
                  .AddKeyedTransient<TestEvent, TestEventHandler2>("3")
                  .AddKeyedSingleton<TestEventHandled, EventsRecorder>("orchestrator")
                  .WithMaxConcurrentHandlers(2));

        var services = serviceCollection.BuildServiceProvider(true);

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var orchestrator = (EventsRecorder)scope.ServiceProvider.GetRequiredKeyedService<IEventHandler<TestEventHandled>>("orchestrator");

        // Act
        var event1 = new TestEvent("Test Event", CorrelationId: "1");

        orchestrator.Expect(
            $"1_{typeof(TestEventHandler).Name}",
            $"1_{typeof(TestEventHandler1).Name}",
            $"1_{typeof(TestEventHandler2).Name}");

        await eventBroker.Publish(event1);

        var completed = await orchestrator.WaitForExpected(timeout: TimeSpan.FromMilliseconds(50));

        // Assert
        Assert.True(completed);
        Assert.Equal(3, orchestrator.HandledEventIds.Length);
        Assert.Contains($"1_{typeof(TestEventHandler).Name}", orchestrator.HandledEventIds);
        Assert.Contains($"1_{typeof(TestEventHandler1).Name}", orchestrator.HandledEventIds);
        Assert.Contains($"1_{typeof(TestEventHandler2).Name}", orchestrator.HandledEventIds);
    }


    [Fact]
    public async Task MultipleKeyedAndDefaultKeyedHandlers_AllExecuted()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddEventBroker(
            x => x.AddKeyedTransient<TestEvent, TestEventHandler>("1")
                  .AddKeyedTransient<TestEvent, TestEventHandler1>()
                  .AddKeyedTransient<TestEvent, TestEventHandler2>("3")
                  .AddKeyedSingleton<TestEventHandled, EventsRecorder>("orchestrator")
                  .WithMaxConcurrentHandlers(2));

        var services = serviceCollection.BuildServiceProvider(true);

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var orchestrator = (EventsRecorder)scope.ServiceProvider.GetRequiredKeyedService<IEventHandler<TestEventHandled>>("orchestrator");

        // Act
        var event1 = new TestEvent("Test Event", CorrelationId: "1");

        orchestrator.Expect(
            $"1_{typeof(TestEventHandler).Name}",
            $"1_{typeof(TestEventHandler1).Name}",
            $"1_{typeof(TestEventHandler2).Name}");

        await eventBroker.Publish(event1);

        var completed = await orchestrator.WaitForExpected(timeout: TimeSpan.FromMilliseconds(50));

        // Assert
        Assert.True(completed);
        Assert.Equal(3, orchestrator.HandledEventIds.Length);
        Assert.Contains($"1_{typeof(TestEventHandler).Name}", orchestrator.HandledEventIds);
        Assert.Contains($"1_{typeof(TestEventHandler1).Name}", orchestrator.HandledEventIds);
        Assert.Contains($"1_{typeof(TestEventHandler2).Name}", orchestrator.HandledEventIds);
    }

    public record TestEvent(string Message, string CorrelationId) : ITraceable<string>;

    public record TestEventHandled(string CorrelationId) : ITraceable<string>;

    public class EventsRecorder : Orchestrator<string, TestEventHandled>
    {
        private readonly ConcurrentBag<(string id, long tick)> _events = new();

        public override async Task Handle(TestEventHandled @event)
        {
            await base.Handle(@event);
            _events.Add((@event.CorrelationId, DateTime.UtcNow.Ticks));
        }

        public string[] HandledEventIds => _events.OrderBy(x => x.tick).Select(x => x.id).ToArray();
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
            var handled = new TestEventHandled(CorrelationId: $"{@event.CorrelationId}_{GetType().Name}");
            await _eventBroker.Publish(handled);
        }

        public Task OnError(Exception exception, TestEvent @event)
        {
            throw new NotImplementedException();
        }
    }

    public class TestEventHandler1 : TestEventHandler
    {
        public TestEventHandler1(IEventBroker eventBroker) : base(eventBroker)
        {
        }
    }

    public class TestEventHandler2 : TestEventHandler
    {
        public TestEventHandler2(IEventBroker eventBroker) : base(eventBroker)
        {
        }
    }
}
