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

        Assert.Equal("Value should be greater than zero (Parameter 'maxConcurrentHandlers')", exception.Message);
    }

    [Fact]
    public void MaxConcurrentHandlers_SetTo_Negative_Throws()
    {
        var serviceCollection = new ServiceCollection();
        var rand = new Random();
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            paramName: "maxConcurrentHandlers",
            testCode: () => serviceCollection.AddEventBroker(x => x.WithMaxConcurrentHandlers(rand.Next(int.MinValue, -1))));

        Assert.Equal("Value should be greater than zero (Parameter 'maxConcurrentHandlers')", exception.Message);
    }

    public record TestEvent();

    public static class Handlers
    {
        public static Action<EventHandlerRegistryBuilder> Registration =>
            x => x.AddKeyedScoped<TestEvent, TestEventHandler>()
                  .AddKeyedSingleton<TestEvent, TestEventHandler1>()
                  .AddKeyedTransient<TestEvent, TestEventHandler2>();
    }

    public class TestEventHandler : IEventHandler<TestEvent>
    {
        public Task Handle(TestEvent @event) => throw new NotImplementedException();

        public Task OnError(Exception exception, TestEvent @event) => throw new NotImplementedException();
    }

    public class TestEventHandler1 : TestEventHandler
    {
    }

    public class TestEventHandler2 : TestEventHandler
    {
    }
}
