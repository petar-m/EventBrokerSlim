using System;
using System.Collections.Generic;
using M.EventBrokerSlim.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace M.EventBrokerSlim.DependencyInjection;

/// <summary>
/// Registers EventBroker event handlers in DI container.
/// </summary>
public class EventHandlerRegistryBuilder
{
    private readonly IServiceCollection _services;
    private readonly List<EventHandlerDescriptor> _eventsHandlersDescriptors = new();

    internal EventHandlerRegistryBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Adds a scoped service implementing <see cref="IEventHandler{TEvent}"/> to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <typeparam name="TEvent">The type of the event handled.</typeparam>
    /// <typeparam name="THandler">The type of the <see cref="IEventHandler{TEvent}"/> implementation.</typeparam>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public EventHandlerRegistryBuilder AddScoped<TEvent, THandler>() where THandler : class, IEventHandler<TEvent>
    {
        var eventHandlerKey = Guid.NewGuid();
        _services.AddKeyedScoped<IEventHandler<TEvent>, THandler>(eventHandlerKey);

        CreateEventHandlerDescriptor<TEvent, THandler>(eventHandlerKey);
        return this;
    }

    /// <summary>
    /// Adds a singleton service implementing <see cref="IEventHandler{TEvent}"/> to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <typeparam name="TEvent">The type of the event handled.</typeparam>
    /// <typeparam name="THandler">The type of the <see cref="IEventHandler{TEvent}"/> implementation.</typeparam>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public EventHandlerRegistryBuilder AddSingleton<TEvent, THandler>() where THandler : class, IEventHandler<TEvent>
    {
        var eventHandlerKey = Guid.NewGuid();
        _services.AddKeyedSingleton<IEventHandler<TEvent>, THandler>(eventHandlerKey);

        CreateEventHandlerDescriptor<TEvent, THandler>(eventHandlerKey);
        return this;
    }

    /// <summary>
    /// Adds a transient service implementing <see cref="IEventHandler{TEvent}"/> to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <typeparam name="TEvent">The type of the event handled.</typeparam>
    /// <typeparam name="THandler">The type of the <see cref="IEventHandler{TEvent}"/> implementation.</typeparam>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public EventHandlerRegistryBuilder AddTransient<TEvent, THandler>() where THandler : class, IEventHandler<TEvent>
    {
        var eventHandlerKey = Guid.NewGuid();
        _services.AddKeyedTransient<IEventHandler<TEvent>, THandler>(eventHandlerKey);

        CreateEventHandlerDescriptor<TEvent, THandler>(eventHandlerKey);
        return this;
    }

    internal void CreateEventHandlerDescriptor<TEvent, THandler>(Guid eventHandlerKey) where THandler : class, IEventHandler<TEvent>
    {
        var descriptor = new EventHandlerDescriptor(
            Key: eventHandlerKey,
            EventType: typeof(TEvent),
            InterfaceType: typeof(IEventHandler<TEvent>),
            Handle: async (handler, @event, retryPolicy, ct) => await ((THandler)handler).Handle((TEvent)@event, retryPolicy, ct),
            OnError: async (handler, @event, exception, retryPolicy, ct) => await ((THandler)handler).OnError(exception, (TEvent)@event, retryPolicy, ct));

        _eventsHandlersDescriptors.Add(descriptor);
    }

    internal static EventHandlerRegistry Build(IEnumerable<EventHandlerRegistryBuilder> builders)
    {
        EventBrokerBuilder? eventBrokerBuilder = null;
        List<EventHandlerDescriptor> descriptors = new();
        foreach(var builder in builders)
        {
            if(builder is EventBrokerBuilder)
            {
                eventBrokerBuilder = (EventBrokerBuilder)builder;
            }

            foreach(var descriptor in builder._eventsHandlersDescriptors)
            {
                descriptors.Add(descriptor);
            }
        }

#pragma warning disable CS8602 // Dereference of a possibly null reference. No EventBrokerBuilder means IServiceCollection.AddEventBroker() was never called, container will trying to resove IEventBroker.
        return new EventHandlerRegistry(descriptors, eventBrokerBuilder._maxConcurrentHandlers, eventBrokerBuilder._disableMissingHandlerWarningLog);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
    }
}
