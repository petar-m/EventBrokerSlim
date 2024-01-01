using System;
using System.Collections.Generic;
using System.Linq;
using M.EventBrokerSlim.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace M.EventBrokerSlim.DependencyInjection;

/// <summary>
/// Registers EventBorker event handlers in DI container.
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
    /// The service key is maintained internally.
    /// </summary>
    /// <typeparam name="TEvent">The type of the event handled.</typeparam>
    /// <typeparam name="THandler">The type of the <see cref="IEventHandler{TEvent}"/> implementation.</typeparam>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public EventHandlerRegistryBuilder AddKeyedScoped<TEvent, THandler>() where THandler : class, IEventHandler<TEvent>
    {
        var eventHandlerKey = Guid.NewGuid();
        _services.AddKeyedScoped<IEventHandler<TEvent>, THandler>(eventHandlerKey);

        CreateEventHandlerDescriptor<TEvent, THandler>(eventHandlerKey);
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
        var eventHandlerKey = Guid.NewGuid();
        _services.AddKeyedSingleton<IEventHandler<TEvent>, THandler>(eventHandlerKey);

        CreateEventHandlerDescriptor<TEvent, THandler>(eventHandlerKey);
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
            Handle: async (handler, @event) => await ((THandler)handler).Handle((TEvent)@event),
            OnError: async (handler, @event, exception) => await ((THandler)handler).OnError(exception, (TEvent)@event));

        _eventsHandlersDescriptors.Add(descriptor);
    }

    internal static EventHandlerRegistry Build(IEnumerable<EventHandlerRegistryBuilder> builders)
    {
        var eventHandlerRegistry = new EventHandlerRegistry();
        foreach (var builer in builders)
        {
            foreach (var descriptor in builer._eventsHandlersDescriptors)
            {
                eventHandlerRegistry.AddHandlerDescriptor(descriptor);
            }
        }

        var eventBrokerBuilder = (EventBrokerBuilder)builders.Single(x => x is EventBrokerBuilder);
        eventHandlerRegistry.DisableMissingHandlerWarningLog = eventBrokerBuilder._disableMissingHandlerWarningLog;
        eventHandlerRegistry.MaxConcurrentHandlers = eventBrokerBuilder._maxConcurrentHandlers;

        return eventHandlerRegistry;
    }
}
