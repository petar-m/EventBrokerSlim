using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using M.EventBrokerSlim.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace M.EventBrokerSlim.Tests;

public class HandlerScopeAndInstanceTests
{
    [Fact]
    public async Task Handler_RegisteredAsTransient_Executed_ByDifferentInstances_And_DifferentScopes()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddEventBroker(
            x => x.AddKeyedTransient<TestEvent, TestEventHandler>()
                  .AddKeyedSingleton<TestEventHandled, HandlerInstanceRecorder>("orchestrator")
                  .WithMaxConcurrentHandlers(1));

        var services = serviceCollection.BuildServiceProvider(true);

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var orchestrator = (HandlerInstanceRecorder)scope.ServiceProvider.GetRequiredKeyedService<IEventHandler<TestEventHandled>>("orchestrator");

        // Act
        orchestrator.Begin(9999);
        var event1 = new TestEvent("Test Event", Id: 1, CorrelationId: 9999);
        var event2 = event1 with { Id = 2 };
        orchestrator.Expect(new[] { event1, event2 });
        
        await eventBroker.Publish(event1);
        await eventBroker.Publish(event2);

        var completed = await orchestrator.Complete(timeout: TimeSpan.FromMilliseconds(50));

        // Assert
        Assert.True(completed);
        // different handler instances
        Assert.Equal(2, orchestrator.HandlerObjectsHashCodes.Length);
        Assert.NotEqual(orchestrator.HandlerObjectsHashCodes[0], orchestrator.HandlerObjectsHashCodes[1]);
        // different scopes
        Assert.Equal(2, orchestrator.HandlerScopeHashCodes.Length);
        Assert.NotEqual(orchestrator.HandlerScopeHashCodes[0], orchestrator.HandlerScopeHashCodes[1]);
    }

    [Fact]
    public async Task Handler_RegisteredAsSingleton_Executed_BySameInstance()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddEventBroker(
            x => x.AddKeyedSingleton<TestEvent, TestEventHandler>()
                  .AddKeyedSingleton<TestEventHandled, HandlerInstanceRecorder>("orchestrator")
                  .WithMaxConcurrentHandlers(1));

        var services = serviceCollection.BuildServiceProvider(true);

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var orchestrator = (HandlerInstanceRecorder)scope.ServiceProvider.GetRequiredKeyedService<IEventHandler<TestEventHandled>>("orchestrator");

        // Act
        orchestrator.Begin(9999);
        var event1 = new TestEvent("Test Event", Id: 1, CorrelationId: 9999);
        var event2 = event1 with { Id = 2 };
        orchestrator.Expect(new[] { event1, event2 });

        await eventBroker.Publish(event1);
        await eventBroker.Publish(event2);

        var completed = await orchestrator.Complete(timeout: TimeSpan.FromMilliseconds(50));

        // Assert
        Assert.True(completed);
        // same handler instances
        Assert.Equal(2, orchestrator.HandlerObjectsHashCodes.Length);
        Assert.Equal(orchestrator.HandlerObjectsHashCodes[0], orchestrator.HandlerObjectsHashCodes[1]);
    }

    [Fact]
    public async Task Handler_RegisteredAsScoped_Executed_ByDifferentInstances_And_DifferentScopes()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddEventBroker(
            x => x.AddKeyedTransient<TestEvent, TestEventHandler>()
                  .AddKeyedSingleton<TestEventHandled, HandlerInstanceRecorder>("orchestrator")
                  .WithMaxConcurrentHandlers(1));

        var services = serviceCollection.BuildServiceProvider(true);

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var orchestrator = (HandlerInstanceRecorder)scope.ServiceProvider.GetRequiredKeyedService<IEventHandler<TestEventHandled>>("orchestrator");

        // Act
        orchestrator.Begin(9999);
        var event1 = new TestEvent("Test Event", Id: 1, CorrelationId: 9999);
        var event2 = event1 with { Id = 2 };
        orchestrator.Expect(new[] { event1, event2 });

        await eventBroker.Publish(event1);
        await eventBroker.Publish(event2);

        var completed = await orchestrator.Complete(timeout: TimeSpan.FromMilliseconds(50));

        // Assert
        Assert.True(completed);
        // different handler instances
        Assert.Equal(2, orchestrator.HandlerObjectsHashCodes.Length);
        Assert.NotEqual(orchestrator.HandlerObjectsHashCodes[0], orchestrator.HandlerObjectsHashCodes[1]);
        // different scopes
        Assert.Equal(2, orchestrator.HandlerScopeHashCodes.Length);
        Assert.NotEqual(orchestrator.HandlerScopeHashCodes[0], orchestrator.HandlerScopeHashCodes[1]);
    }

    public record TestEvent(string Message, int Id, int CorrelationId) : ITraceableEvent<int>, IIdentifieableEvent<int>;

    public record TestEventHandled(int Id, int CorrelationId, int HandlerObjectHashCode, int HandlerScopeHashCode) : ITraceableEvent<int>, IIdentifieableEvent<int>;

    public class HandlerInstanceRecorder : Orchestrator<int, TestEventHandled>
    {
        private readonly ConcurrentBag<(int id, long tick)> _handlerInstances = new();
        private readonly ConcurrentBag<(int id, long tick)> _scopeInstances = new();

        public override async Task Handle(TestEventHandled @event)
        {
            await base.Handle(@event);
            _handlerInstances.Add((@event.HandlerObjectHashCode, DateTime.UtcNow.Ticks));
            _scopeInstances.Add((@event.HandlerScopeHashCode, DateTime.UtcNow.Ticks));
        }

        public int[] HandlerObjectsHashCodes => _handlerInstances.OrderBy(x => x.tick).Select(x => x.id).ToArray();

        public int[] HandlerScopeHashCodes => _scopeInstances.OrderBy(x => x.tick).Select(x => x.id).ToArray();
    }

    public class TestEventHandler : IEventHandler<TestEvent>
    {
        private readonly IEventBroker _eventBroker;
        private readonly IServiceProvider _serviceProvider;

        public TestEventHandler(IEventBroker eventBroker, IServiceProvider serviceScope)
        {
            _eventBroker = eventBroker;
            _serviceProvider = serviceScope;
        }

        public async Task Handle(TestEvent @event)
        {
            var handled = new TestEventHandled(
                Id: @event.Id, 
                CorrelationId: @event.CorrelationId, 
                HandlerObjectHashCode: GetHashCode(),
                HandlerScopeHashCode: _serviceProvider.GetHashCode());
            
            await _eventBroker.Publish(handled);
        }

        public Task OnError(Exception exception, TestEvent @event)
        {
            throw new NotImplementedException();
        }
    }
}
