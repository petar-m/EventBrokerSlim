using System;
using M.EventBrokerSlim.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace M.EventBrokerSlim.DependencyInjection;

public class EventHandlerRegistryBuilder
{
    private readonly EventHandlerRegistry _registry = new();
    private readonly IServiceCollection _services;

    public EventHandlerRegistryBuilder(IServiceCollection services)
    {
        _services = services;
    }

    public EventHandlerRegistryBuilder WithMaxConcurrentHandlers(int maxConcurrentHandlers)
    {
        _registry.WithMaxConcurrentHandlers(maxConcurrentHandlers);
        return this;
    }

    public EventHandlerRegistryBuilder AddKeyedScoped<TEvent, THandler>(string? eventHandlerKey = null) where THandler : class, IEventHandler<TEvent>
    {
        eventHandlerKey = GetOrCreateKey<THandler>(eventHandlerKey);
        _services.AddKeyedScoped<IEventHandler<TEvent>, THandler>(eventHandlerKey);
        _registry.RegisterHandlerDescriptor<TEvent, THandler>(eventHandlerKey);
        return this;
    }

    public EventHandlerRegistryBuilder AddKeyedSingleton<TEvent, THandler>(string? eventHandlerKey = null) where THandler : class, IEventHandler<TEvent>
    {
        eventHandlerKey = GetOrCreateKey<THandler>(eventHandlerKey);
        _services.AddKeyedSingleton<IEventHandler<TEvent>, THandler>(eventHandlerKey);
        _registry.RegisterHandlerDescriptor<TEvent, THandler>(eventHandlerKey);
        return this;
    }

    public EventHandlerRegistryBuilder AddKeyedTransient<TEvent, THandler>(string? eventHandlerKey = null) where THandler : class, IEventHandler<TEvent>
    {
        eventHandlerKey = GetOrCreateKey<THandler>(eventHandlerKey);
        _services.AddKeyedTransient<IEventHandler<TEvent>, THandler>(eventHandlerKey);
        _registry.RegisterHandlerDescriptor<TEvent, THandler>(eventHandlerKey);
        return this;
    }

    public EventHandlerRegistryBuilder Add(Action<EventHandlerRegistryBuilder> configure)
    {
        configure(this);
        return this;
    }

    private static string GetOrCreateKey<THandler>(string? key) => key ?? typeof(THandler).FullName!;

    internal EventHandlerRegistry Build()
    {
        return _registry;
    }
}
