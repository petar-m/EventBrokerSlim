using System;
using M.EventBrokerSlim.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace M.EventBrokerSlim.DependencyInjection;

public sealed class EventHandlerRegistryBuilder
{
    private readonly EventHandlerRegistry _registry = new();
    private readonly IServiceCollection _services;

    public EventHandlerRegistryBuilder(IServiceCollection services)
    {
        _services = services;
    }

    public EventHandlerRegistryBuilder WithMaxConcurrentHandlers(int maxConcurrentHandlers)
    {
        _registry.MaxConcurrentHandlers = maxConcurrentHandlers;
        return this;
    }

    public EventHandlerRegistryBuilder DisableMissingHandlerWarningLog()
    {
        _registry.DisableMissingHandlerWarningLog = true;
        return this;
    }

    public EventHandlerRegistryBuilder AddKeyedScoped<TEvent, THandler>() where THandler : class, IEventHandler<TEvent>
    {
        var eventHandlerKey = Guid.NewGuid().ToString();
        _services.AddKeyedScoped<IEventHandler<TEvent>, THandler>(eventHandlerKey);
        _registry.RegisterHandlerDescriptor<TEvent, THandler>(eventHandlerKey);
        return this;
    }

    public EventHandlerRegistryBuilder AddKeyedSingleton<TEvent, THandler>() where THandler : class, IEventHandler<TEvent>
    {
        var eventHandlerKey = Guid.NewGuid().ToString();
        _services.AddKeyedSingleton<IEventHandler<TEvent>, THandler>(eventHandlerKey);
        _registry.RegisterHandlerDescriptor<TEvent, THandler>(eventHandlerKey);
        return this;
    }

    public EventHandlerRegistryBuilder AddKeyedTransient<TEvent, THandler>() where THandler : class, IEventHandler<TEvent>
    {
        var eventHandlerKey = Guid.NewGuid().ToString();
        _services.AddKeyedTransient<IEventHandler<TEvent>, THandler>(eventHandlerKey);
        _registry.RegisterHandlerDescriptor<TEvent, THandler>(eventHandlerKey);
        return this;
    }

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
