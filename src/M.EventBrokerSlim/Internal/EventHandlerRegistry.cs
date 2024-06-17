using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace M.EventBrokerSlim.Internal;

internal sealed class EventHandlerRegistry
{
    private readonly FrozenDictionary<Type, ImmutableArray<EventHandlerDescriptor>> _eventHandlerDescriptors;

    public EventHandlerRegistry(List<EventHandlerDescriptor> descriptors, int maxConcurrentHandlers, bool disableMissingHandlerWarningLog)
    {
        _eventHandlerDescriptors = descriptors.GroupBy(x => x.EventType)
                                              .ToFrozenDictionary(x => x.Key, x => x.ToImmutableArray());
        MaxConcurrentHandlers = maxConcurrentHandlers;
        DisableMissingHandlerWarningLog = disableMissingHandlerWarningLog;
    }

    internal int MaxConcurrentHandlers { get; }

    internal bool DisableMissingHandlerWarningLog { get; }

    internal ImmutableArray<EventHandlerDescriptor> GetEventHandlers(Type eventType)
    {
        _eventHandlerDescriptors.TryGetValue(eventType, out var handlers);
        return handlers.IsDefault ? ImmutableArray<EventHandlerDescriptor>.Empty : handlers;
    }
}
