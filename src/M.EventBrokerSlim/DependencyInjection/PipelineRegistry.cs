using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Enfolder;

namespace M.EventBrokerSlim.DependencyInjection;

public class PipelineRegistry
{
    private readonly FrozenDictionary<Type, ImmutableArray<IPipeline>> _pipelines;

    public PipelineRegistry(IEnumerable<EventPipeline> pipelines, IServiceProvider? serviceProvider = null)
    {
        _pipelines = pipelines
            .Select(x =>
            {
                x.Pipeline.ServiceProvider ??= serviceProvider;
                return x;
            })
            .GroupBy(x => x.Event)
            .ToFrozenDictionary(
                x => x.Key,
                x => x.Select(y => y.Pipeline).ToImmutableArray());
    }

    public ImmutableArray<IPipeline> Get(Type eventType)
    {
        if(_pipelines.TryGetValue(eventType, out ImmutableArray<IPipeline> pipelines))
        {
            return pipelines;
        }

        return ImmutableArray<IPipeline>.Empty;
    }
}
