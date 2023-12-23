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

    public EventHandlerRegistryBuilder AddKeyedScoped<TEvent, THandler>(string? key = null) where THandler : class, IEventHandler<TEvent>
    {
        key = GetOrCreateKey<TEvent>(key);
        _services.AddKeyedScoped<IEventHandler<TEvent>, THandler>(key);
        _registry.RegisterHandlerDescriptor<TEvent, THandler>(key);
        return this;
    }

    public EventHandlerRegistryBuilder AddKeyedSingleton<TEvent, THandler>(string? key = null) where THandler : class, IEventHandler<TEvent>
    {
        key = GetOrCreateKey<TEvent>(key);
        _services.AddKeyedSingleton<IEventHandler<TEvent>, THandler>(key);
        _registry.RegisterHandlerDescriptor<TEvent, THandler>(key);
        return this;
    }

    public EventHandlerRegistryBuilder AddKeyedTransient<TEvent, THandler>(string? key = null) where THandler : class, IEventHandler<TEvent>
    {
        key = GetOrCreateKey<TEvent>(key);
        _services.AddKeyedTransient<IEventHandler<TEvent>, THandler>(key);
        _registry.RegisterHandlerDescriptor<TEvent, THandler>(key);
        return this;
    }

    public EventHandlerRegistryBuilder Add(Action<EventHandlerRegistryBuilder> configure)
    {
        configure(this);
        return this;
    }

    private static string GetOrCreateKey<TEvent>(string? key) => key ?? typeof(TEvent).FullName!;

    internal EventHandlerRegistry Build()
    {
        return _registry;
    }
}
