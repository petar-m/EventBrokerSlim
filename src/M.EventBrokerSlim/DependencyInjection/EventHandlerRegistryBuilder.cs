using System;
using M.EventBrokerSlim.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace M.EventBrokerSlim.DependencyInjection;

/// <summary>
/// Registers event handlers in DI container and configures event broker behavior.
/// </summary>
public sealed class EventHandlerRegistryBuilder
{
    private readonly EventHandlerRegistry _registry = new();
    private readonly IServiceCollection _services;

    internal EventHandlerRegistryBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Sets the maximum number of event handlers to run at the same time.
    /// </summary>
    /// <param name="maxConcurrentHandlers">Maximum number of event handlers to run at the same time. Default is 2.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public EventHandlerRegistryBuilder WithMaxConcurrentHandlers(int maxConcurrentHandlers)
    {
        _registry.MaxConcurrentHandlers = maxConcurrentHandlers;
        return this;
    }

    /// <summary>
    /// Turns off Warning log when no handler is found for event. Turned on by default.
    /// </summary>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public EventHandlerRegistryBuilder DisableMissingHandlerWarningLog()
    {
        _registry.DisableMissingHandlerWarningLog = true;
        return this;
    }

    /// <summary>
    /// Adds a scoped service implementing <see cref="IEventHandler{TEvent}"/> to the specified <see cref="IServiceCollection"/>.
    /// The service key is maintained internally.
    /// </summary>
    /// <typeparam name="TEvent">The type of the event handled.</typeparam>
    /// <typeparam name="THandler">The type of the <see cref="IEventHandler{TEvent}"/> implementation.</typeparam>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public EventHandlerRegistryBuilder AddKeyedScoped<TEvent, THandler>() where THandler : class, IEventHandler<TEvent>
    {
        var eventHandlerKey = Guid.NewGuid().ToString();
        _services.AddKeyedScoped<IEventHandler<TEvent>, THandler>(eventHandlerKey);
        _registry.RegisterHandlerDescriptor<TEvent, THandler>(eventHandlerKey);
        return this;
    }

    /// <summary>
    /// Adds a singleton service implementing <see cref="IEventHandler{TEvent}"/> to the specified <see cref="IServiceCollection"/>.
    /// The service key is maintained internally.
    /// </summary>
    /// <typeparam name="TEvent">The type of the event handled.</typeparam>
    /// <typeparam name="THandler">The type of the <see cref="IEventHandler{TEvent}"/> implementation.</typeparam>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public EventHandlerRegistryBuilder AddKeyedSingleton<TEvent, THandler>() where THandler : class, IEventHandler<TEvent>
    {
        var eventHandlerKey = Guid.NewGuid().ToString();
        _services.AddKeyedSingleton<IEventHandler<TEvent>, THandler>(eventHandlerKey);
        _registry.RegisterHandlerDescriptor<TEvent, THandler>(eventHandlerKey);
        return this;
    }

    /// <summary>
    /// Adds a transient service implementing <see cref="IEventHandler{TEvent}"/> to the specified <see cref="IServiceCollection"/>.
    /// The service key is maintained internally.
    /// </summary>
    /// <typeparam name="TEvent">The type of the event handled.</typeparam>
    /// <typeparam name="THandler">The type of the <see cref="IEventHandler{TEvent}"/> implementation.</typeparam>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public EventHandlerRegistryBuilder AddKeyedTransient<TEvent, THandler>() where THandler : class, IEventHandler<TEvent>
    {
        var eventHandlerKey = Guid.NewGuid().ToString();
        _services.AddKeyedTransient<IEventHandler<TEvent>, THandler>(eventHandlerKey);
        _registry.RegisterHandlerDescriptor<TEvent, THandler>(eventHandlerKey);
        return this;
    }

    /// <summary>
    /// Accepts a action for configuring event handlers and behavior of the event broker.
    /// </summary>
    /// <param name="configure"></param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public EventHandlerRegistryBuilder Add(Action<EventHandlerRegistryBuilder> configure)
    {
        configure(this);
        return this;
    }

    internal EventHandlerRegistry Build()
    {
        return _registry;
    }
}
