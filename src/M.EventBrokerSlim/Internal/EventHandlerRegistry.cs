using System;
using System.Collections.Generic;

namespace M.EventBrokerSlim.Internal;

internal sealed class EventHandlerRegistry
{
    private readonly Dictionary<Type, List<EventHandlerDescriptor>> _eventHandlerDescriptors = new();

    internal int MaxConcurrentHandlers { get; set; } = 2;

    internal bool DisableMissingHandlerWarningLog { get; set; }

    internal List<EventHandlerDescriptor>? GetEventHandlers(Type eventType)
    {
        _eventHandlerDescriptors.TryGetValue(eventType, out var handlers);
        return handlers;
    }

    internal void AddHandlerDescriptor(EventHandlerDescriptor eventHandlerDescriptor)
    {
        if (!_eventHandlerDescriptors.TryGetValue(eventHandlerDescriptor.EventType, out var handlers))
        {
            handlers = new List<EventHandlerDescriptor>();
            _eventHandlerDescriptors.Add(eventHandlerDescriptor.EventType, handlers);
        }

        handlers.Add(eventHandlerDescriptor);
    }
}
