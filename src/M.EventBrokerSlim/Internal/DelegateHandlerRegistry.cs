using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace M.EventBrokerSlim.Internal;

internal sealed class DelegateHandlerRegistry
{
    private readonly FrozenDictionary<Type, ImmutableArray<DelegateHandlerDescriptor>> _handlers;

    public DelegateHandlerRegistry(IEnumerable<DelegateHandlerDescriptor> delegateHandlerDescriptors)
    {
        _handlers = delegateHandlerDescriptors
            .GroupBy(x => x.EventType)
            .ToFrozenDictionary(x => x.Key, x => x.ToImmutableArray());
    }

    public ImmutableArray<DelegateHandlerDescriptor> GetHandlers(Type eventType)
    {
        _handlers.TryGetValue(eventType, out ImmutableArray<DelegateHandlerDescriptor> handlers);
        return handlers.IsDefaultOrEmpty ? ImmutableArray<DelegateHandlerDescriptor>.Empty : handlers;
    }

    internal int MaxPipelineLength()
    {
        if(_handlers.Count == 0)
        {
            return 0;
        }

        return _handlers.SelectMany(x => x.Value.Select(p => p.Pipeline.Count)).Max() + 1;
    }
}
