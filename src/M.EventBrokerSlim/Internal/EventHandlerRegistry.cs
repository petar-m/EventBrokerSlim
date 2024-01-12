using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;

namespace M.EventBrokerSlim.Internal;

internal sealed class EventHandlerRegistry
{
    private readonly FrozenDictionary<Type, List<EventHandlerDescriptor>> _eventHandlerDescriptors;

    public EventHandlerRegistry(List<EventHandlerDescriptor> descriptors, int maxConcurrentHandlers, bool disableMissingHandlerWarningLog)
    {
        _eventHandlerDescriptors = descriptors.GroupBy(x => x.EventType)
                                              .ToFrozenDictionary(x => x.Key, x => x.ToList());
        MaxConcurrentHandlers = maxConcurrentHandlers;
        DisableMissingHandlerWarningLog = disableMissingHandlerWarningLog;
    }

    internal int MaxConcurrentHandlers { get; }

    internal bool DisableMissingHandlerWarningLog { get; }

    internal List<EventHandlerDescriptor>? GetEventHandlers(Type eventType)
    {
        _eventHandlerDescriptors.TryGetValue(eventType, out var handlers);
        return handlers;
    }
}
