using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using FuncPipeline;
using Microsoft.Extensions.DependencyInjection;

namespace M.EventBrokerSlim.DependencyInjection;

public class PipelineRegistry
{
    private readonly FrozenDictionary<Type, ImmutableArray<IPipeline>> _pipelines;

    public PipelineRegistry(IEnumerable<EventPipeline> pipelines, IServiceScopeFactory? serviceScopeFactory = null)
    {
        _pipelines = pipelines
            .Select(x =>
            {
                x.Pipeline.ServiceScopeFactory ??= serviceScopeFactory;
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
