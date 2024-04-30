using System;
using System.Threading;
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
        var services = ServiceProviderHelper.BuildWithEventsRecorder<int>(
            sc => sc.AddEventBroker(
                        x => x.WithMaxConcurrentHandlers(1)
                              .AddKeyedTransient<TestEvent, TestEventHandler>()));

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsRecorder = scope.ServiceProvider.GetRequiredService<EventsRecorder<int>>();

        // Act
        var event1 = new TestEvent(CorrelationId: 1);
        var event2 = event1 with { CorrelationId = 2 };
        eventsRecorder.Expect(event1, event2);

        await eventBroker.Publish(event1);
        await eventBroker.Publish(event2);

        var completed = await eventsRecorder.WaitForExpected(timeout: TimeSpan.FromMilliseconds(50));

        // Assert
        Assert.True(completed);
        // different handler instances
        Assert.Equal(2, eventsRecorder.HandlerObjectsHashCodes.Length);
        Assert.NotEqual(eventsRecorder.HandlerObjectsHashCodes[0], eventsRecorder.HandlerObjectsHashCodes[1]);
        // different scopes
        Assert.Equal(2, eventsRecorder.HandlerScopeHashCodes.Length);
        Assert.NotEqual(eventsRecorder.HandlerScopeHashCodes[0], eventsRecorder.HandlerScopeHashCodes[1]);
    }

    [Fact]
    public async Task Handler_RegisteredAsSingleton_Executed_BySameInstance()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithEventsRecorder<int>(
            sc => sc.AddEventBroker(
                        x => x.WithMaxConcurrentHandlers(1)
                              .AddKeyedSingleton<TestEvent, TestEventHandler>()));

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsRecorder = scope.ServiceProvider.GetRequiredService<EventsRecorder<int>>();

        // Act
        var event1 = new TestEvent(CorrelationId: 1);
        var event2 = event1 with { CorrelationId = 2 };
        eventsRecorder.Expect(event1, event2);

        await eventBroker.Publish(event1);
        await eventBroker.Publish(event2);

        var completed = await eventsRecorder.WaitForExpected(timeout: TimeSpan.FromMilliseconds(50));

        // Assert
        Assert.True(completed);
        // same handler instances
        Assert.Equal(2, eventsRecorder.HandlerObjectsHashCodes.Length);
        Assert.Equal(eventsRecorder.HandlerObjectsHashCodes[0], eventsRecorder.HandlerObjectsHashCodes[1]);
        // same scope (singletons resolve from root scope)
        Assert.Equal(2, eventsRecorder.HandlerScopeHashCodes.Length);
        Assert.Equal(eventsRecorder.HandlerScopeHashCodes[0], eventsRecorder.HandlerScopeHashCodes[1]);
    }

    [Fact]
    public async Task Handler_RegisteredAsScoped_Executed_ByDifferentInstances_And_DifferentScopes()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithEventsRecorder<int>(
            sc => sc.AddEventBroker(
                        x => x.WithMaxConcurrentHandlers(1)
                              .AddKeyedScoped<TestEvent, TestEventHandler>()));

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsRecorder = scope.ServiceProvider.GetRequiredService<EventsRecorder<int>>();

        // Act
        var event1 = new TestEvent(CorrelationId: 1);
        var event2 = event1 with { CorrelationId = 2 };
        eventsRecorder.Expect(event1, event2);

        await eventBroker.Publish(event1);
        await eventBroker.Publish(event2);

        var completed = await eventsRecorder.WaitForExpected(timeout: TimeSpan.FromMilliseconds(50));

        // Assert
        Assert.True(completed);
        // different handler instances
        Assert.Equal(2, eventsRecorder.HandlerObjectsHashCodes.Length);
        Assert.NotEqual(eventsRecorder.HandlerObjectsHashCodes[0], eventsRecorder.HandlerObjectsHashCodes[1]);
        // different scopes
        Assert.Equal(2, eventsRecorder.HandlerScopeHashCodes.Length);
        Assert.NotEqual(eventsRecorder.HandlerScopeHashCodes[0], eventsRecorder.HandlerScopeHashCodes[1]);
    }

    public record TestEvent(int CorrelationId) : ITraceable<int>;

    public class TestEventHandler : IEventHandler<TestEvent>
    {
        private readonly EventsRecorder<int> _eventsRecorder;
        private readonly IServiceProvider _scope;

        public TestEventHandler(EventsRecorder<int> eventsRecorder, IServiceProvider scope)
        {
            _eventsRecorder = eventsRecorder;
            _scope = scope;
        }

        public Task Handle(TestEvent @event, RetryPolicy retryPolicy, CancellationToken cancellationToken)
        {
            _eventsRecorder.Notify(@event);
            _eventsRecorder.Notify(@event, handlerInstance: GetHashCode(), scopeInstance: _scope.GetHashCode());
            return Task.CompletedTask;
        }

        public Task OnError(Exception exception, TestEvent @event, RetryPolicy retryPolicy, CancellationToken cancellationToken)
        {
            _eventsRecorder.Notify(exception, @event);
            return Task.CompletedTask;
        }
    }
}
