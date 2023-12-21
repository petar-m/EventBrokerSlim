using System;
using System.Collections.Generic;

namespace M.EventBrokerSlim.Internal;

internal class EventHandlerRegistry
{
    private readonly Dictionary<Type, List<EventHandlerDescriptor>> _eventHandlerDescriptors = new();

    public int MaxConcurrentHandlers { get; internal set; } = 2;

    public EventHandlerRegistry WithMaxConcurrentHandlers(int maxConcurrentHandlers)
    {
        if (maxConcurrentHandlers <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxConcurrentHandlers), "Value should be greater than zero");
        }

        MaxConcurrentHandlers = maxConcurrentHandlers;
        return this;
    }

    internal List<EventHandlerDescriptor>? GetEventHandlers(Type eventType)
    {
        _eventHandlerDescriptors.TryGetValue(eventType, out var handlers);
        return handlers;
    }

    public void RegisterHandlerDescriptor<TEvent, THandler>(string key) where THandler : class, IEventHandler<TEvent>
    {
        var descriptor = new EventHandlerDescriptor(
                    Key: key,
                    InterfaceType: typeof(IEventHandler<TEvent>),
                    Handle: async (handler, @event) => await ((THandler)handler).Handle((TEvent)@event),
                    ShouldHandle: async (handler, @event) => await ((THandler)handler).ShouldHandle((TEvent)@event),
                    OnError: async (handler, @event, exception) => await ((THandler)handler).OnError(exception, (TEvent)@event));

        if (!_eventHandlerDescriptors.TryGetValue(typeof(TEvent), out var handlers))
        {
            handlers = new List<EventHandlerDescriptor>();
            _eventHandlerDescriptors.Add(typeof(TEvent), handlers);
        }

        handlers.Add(descriptor);
    }
}
