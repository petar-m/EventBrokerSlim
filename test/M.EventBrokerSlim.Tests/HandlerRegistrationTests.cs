using System;
using System.Linq;
using System.Threading.Tasks;
using M.EventBrokerSlim.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace M.EventBrokerSlim.Tests;

public class HandlerRegistrationTests
{
    [Fact]
    public void RegisterHandlerAsTransient_AddsToServiceCollection()
    {
        var serviceCollection = new ServiceCollection()
            .AddEventBroker(
                x => x.AddKeyedTransient<TestEvent, TestEventHandler>());

        var serviceDescriptor = serviceCollection.Single(x => x.IsKeyedService && x.KeyedImplementationType == typeof(TestEventHandler));

        Assert.Equal(ServiceLifetime.Transient, serviceDescriptor.Lifetime);
    }

    [Fact]
    public void RegisterHandlerAsScoped_AddsToServiceCollection()
    {
        var serviceCollection = new ServiceCollection()
            .AddEventBroker(
                x => x.AddKeyedScoped<TestEvent, TestEventHandler>());

        var serviceDescriptor = serviceCollection.Single(x => x.IsKeyedService && x.KeyedImplementationType == typeof(TestEventHandler));

        Assert.Equal(ServiceLifetime.Scoped, serviceDescriptor.Lifetime);
    }

    [Fact]
    public void RegisterHandlerAsSingleton_AddsToServiceCollection()
    {
        var serviceCollection = new ServiceCollection()
            .AddEventBroker(
                x => x.AddKeyedSingleton<TestEvent, TestEventHandler>());

        var serviceDescriptor = serviceCollection.Single(x => x.IsKeyedService && x.KeyedImplementationType == typeof(TestEventHandler));

        Assert.Equal(ServiceLifetime.Singleton, serviceDescriptor.Lifetime);
    }

    [Fact]
    public void RegisterHandlersUsingDelegate()
    {
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddEventBroker(x => x.Add(Handlers.Registration));

        var serviceDescriptor = serviceCollection.Single(x => x.IsKeyedService && x.KeyedImplementationType == typeof(TestEventHandler));
        Assert.Equal(ServiceLifetime.Scoped, serviceDescriptor.Lifetime);

        serviceDescriptor = serviceCollection.Single(x => x.IsKeyedService && x.KeyedImplementationType == typeof(TestEventHandler1));
        Assert.Equal(ServiceLifetime.Singleton, serviceDescriptor.Lifetime);

        serviceDescriptor = serviceCollection.Single(x => x.IsKeyedService && x.KeyedImplementationType == typeof(TestEventHandler2));
        Assert.Equal(ServiceLifetime.Transient, serviceDescriptor.Lifetime);
    }

    [Fact]
    public void MaxConcurrentHandlers_SetTo_Zero_Throws()
    {
        var serviceCollection = new ServiceCollection();

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            paramName: "maxConcurrentHandlers",
            testCode: () => serviceCollection.AddEventBroker(x => x.WithMaxConcurrentHandlers(0)));

        Assert.Equal("MaxConcurrentHandlers should be greater than zero (Parameter 'maxConcurrentHandlers')", exception.Message);
    }

    [Fact]
    public void MaxConcurrentHandlers_SetTo_Negative_Throws()
    {
        var serviceCollection = new ServiceCollection();
        var rand = new Random();
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            paramName: "maxConcurrentHandlers",
            testCode: () => serviceCollection.AddEventBroker(x => x.WithMaxConcurrentHandlers(rand.Next(int.MinValue, -1))));

        Assert.Equal("MaxConcurrentHandlers should be greater than zero (Parameter 'maxConcurrentHandlers')", exception.Message);
    }

    [Fact]
    public async Task Handlers_RegisteredWith_AddEventBroker_AreExecuted()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithEventsRecorder<string>(
            sc => sc.AddEventBroker(
                        x => x.AddKeyedTransient<TestEvent, TestEventHandler>()
                              .AddKeyedScoped<TestEvent, TestEventHandler1>()
                              .AddKeyedScoped<TestEvent, TestEventHandler2>()));

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsRecorder = scope.ServiceProvider.GetRequiredService<EventsRecorder<string>>();

        // Act
        var testEvent = new TestEvent(CorrelationId: "1");

        eventsRecorder.Expect(
            $"1_{typeof(TestEventHandler).Name}",
            $"1_{typeof(TestEventHandler1).Name}",
            $"1_{typeof(TestEventHandler2).Name}");

        await eventBroker.Publish(testEvent);

        var completed = await eventsRecorder.WaitForExpected(timeout: TimeSpan.FromMilliseconds(50));

        // Assert
        Assert.True(completed);
    }

    [Fact]
    public async Task Handlers_RegisteredBefore_AddEventBroker_AreExecuted()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithEventsRecorder<string>(
            sc => sc.AddEventHandlers(
                        x => x.AddKeyedTransient<TestEvent, TestEventHandler>()
                              .AddKeyedScoped<TestEvent, TestEventHandler1>()
                              .AddKeyedScoped<TestEvent, TestEventHandler2>())
                    .AddEventBroker());

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsRecorder = scope.ServiceProvider.GetRequiredService<EventsRecorder<string>>();

        // Act
        var testEvent = new TestEvent(CorrelationId: "1");

        eventsRecorder.Expect(
            $"1_{typeof(TestEventHandler).Name}",
            $"1_{typeof(TestEventHandler1).Name}",
            $"1_{typeof(TestEventHandler2).Name}");

        await eventBroker.Publish(testEvent);

        var completed = await eventsRecorder.WaitForExpected(timeout: TimeSpan.FromMilliseconds(50));

        // Assert
        Assert.True(completed);
    }

    [Fact]
    public async Task Handlers_RegisteredAfter_AddEventBroker_AreExecuted()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithEventsRecorder<string>(
            sc => sc.AddEventBroker()
                    .AddEventHandlers(
                        x => x.AddKeyedTransient<TestEvent, TestEventHandler>()
                              .AddKeyedScoped<TestEvent, TestEventHandler1>()
                              .AddKeyedScoped<TestEvent, TestEventHandler2>()));

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsRecorder = scope.ServiceProvider.GetRequiredService<EventsRecorder<string>>();

        // Act
        var testEvent = new TestEvent(CorrelationId: "1");

        eventsRecorder.Expect(
            $"1_{typeof(TestEventHandler).Name}",
            $"1_{typeof(TestEventHandler1).Name}",
            $"1_{typeof(TestEventHandler2).Name}");

        await eventBroker.Publish(testEvent);

        var completed = await eventsRecorder.WaitForExpected(timeout: TimeSpan.FromMilliseconds(50));

        // Assert
        Assert.True(completed);
    }

    [Fact]
    public async Task Handlers_RegisteredBeforeAndAfter_AddEventBroker_AreExecuted()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithEventsRecorder<string>(
            sc => sc.AddEventHandlers(x => x.AddKeyedTransient<TestEvent, TestEventHandler>())
                    .AddEventBroker(x => x.AddKeyedScoped<TestEvent, TestEventHandler1>())
                    .AddEventHandlers(x => x.AddKeyedScoped<TestEvent, TestEventHandler2>()));

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsRecorder = scope.ServiceProvider.GetRequiredService<EventsRecorder<string>>();

        // Act
        var testEvent = new TestEvent(CorrelationId: "1");

        eventsRecorder.Expect(
            $"1_{typeof(TestEventHandler).Name}",
            $"1_{typeof(TestEventHandler1).Name}",
            $"1_{typeof(TestEventHandler2).Name}");

        await eventBroker.Publish(testEvent);

        var completed = await eventsRecorder.WaitForExpected(timeout: TimeSpan.FromMilliseconds(50));

        // Assert
        Assert.True(completed);
    }

    [Fact]
    public async Task Handlers_RegisteredWithDelegate_AreExecuted()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithEventsRecorder<string>(
            sc => sc.AddEventHandlers(Handlers.Registration)
                    .AddEventBroker());

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsRecorder = scope.ServiceProvider.GetRequiredService<EventsRecorder<string>>();

        // Act
        var testEvent = new TestEvent(CorrelationId: "1");

        eventsRecorder.Expect(
            $"1_{typeof(TestEventHandler).Name}",
            $"1_{typeof(TestEventHandler1).Name}",
            $"1_{typeof(TestEventHandler2).Name}");

        await eventBroker.Publish(testEvent);

        var completed = await eventsRecorder.WaitForExpected(timeout: TimeSpan.FromMilliseconds(50));

        // Assert
        Assert.True(completed);
    }

    public static class Handlers
    {
        public static Action<EventHandlerRegistryBuilder> Registration =>
            x => x.AddKeyedScoped<TestEvent, TestEventHandler>()
                  .AddKeyedSingleton<TestEvent, TestEventHandler1>()
                  .AddKeyedTransient<TestEvent, TestEventHandler2>();
    }

    public record TestEvent(string CorrelationId) : ITraceable<string>;

    public class TestEventHandler : IEventHandler<TestEvent>
    {
        private readonly EventsRecorder<string> _eventsRecorder;
        private readonly IServiceProvider _scope;

        public TestEventHandler(EventsRecorder<string> eventsRecorder, IServiceProvider scope)
        {
            _eventsRecorder = eventsRecorder;
            _scope = scope;
        }

        public Task Handle(TestEvent @event)
        {
            _eventsRecorder.Notify($"{@event.CorrelationId}_{GetType().Name}");
            return Task.CompletedTask;
        }

        public Task OnError(Exception exception, TestEvent @event)
        {
            _eventsRecorder.Notify(exception, @event);
            return Task.CompletedTask;
        }
    }

    public class TestEventHandler1 : TestEventHandler
    {
        public TestEventHandler1(EventsRecorder<string> eventsRecorder, IServiceProvider scope) : base(eventsRecorder, scope)
        {
        }
    }

    public class TestEventHandler2 : TestEventHandler
    {
        public TestEventHandler2(EventsRecorder<string> eventsRecorder, IServiceProvider scope) : base(eventsRecorder, scope)
        {
        }
    }
}
