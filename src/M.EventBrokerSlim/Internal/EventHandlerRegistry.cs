using System;
using System.Collections.Generic;

namespace M.EventBrokerSlim.Internal;

internal sealed class EventHandlerRegistry
{
    private readonly Dictionary<Type, List<EventHandlerDescriptor>> _eventHandlerDescriptors = new();
    private int _maxConcurrentHandlers = 2;

    internal int MaxConcurrentHandlers
    {
        get
        {
            return _maxConcurrentHandlers;
        }
        set
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "MaxConcurrentHandlers should be greater than zero");
            }

            _maxConcurrentHandlers = value;
        }
    }

    internal bool DisableMissingHandlerWarningLog { get; set; }

    internal List<EventHandlerDescriptor>? GetEventHandlers(Type eventType)
    {
        _eventHandlerDescriptors.TryGetValue(eventType, out var handlers);
        return handlers;
    }

    internal void RegisterHandlerDescriptor<TEvent, THandler>(string eventHandlerKey) where THandler : class, IEventHandler<TEvent>
    {
        var descriptor = new EventHandlerDescriptor(
                    Key: eventHandlerKey,
                    InterfaceType: typeof(IEventHandler<TEvent>),
                    Handle: async (handler, @event) => await ((THandler)handler).Handle((TEvent)@event),
                    OnError: async (handler, @event, exception) => await ((THandler)handler).OnError(exception, (TEvent)@event));

        if (!_eventHandlerDescriptors.TryGetValue(typeof(TEvent), out var handlers))
        {
            handlers = new List<EventHandlerDescriptor>();
            _eventHandlerDescriptors.Add(typeof(TEvent), handlers);
        }

        handlers.Add(descriptor);
    }
}
