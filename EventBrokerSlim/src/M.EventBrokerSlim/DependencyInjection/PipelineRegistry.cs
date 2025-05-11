using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using FuncPipeline;
using Microsoft.Extensions.DependencyInjection;

namespace M.EventBrokerSlim.DependencyInjection;

/// <summary>
/// Represents a registry for managing event pipelines.
/// </summary>
public class PipelineRegistry
{
    private readonly FrozenDictionary<Type, ImmutableArray<IPipeline>> _pipelines;

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineRegistry"/> class.
    /// </summary>
    /// <param name="pipelines">The collection of event pipelines to register.</param>
    /// <param name="serviceScopeFactory">The optional service scope factory for pipeline services.</param>
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

    /// <summary>
    /// Retrieves the pipelines associated with the specified event type.
    /// </summary>
    /// <param name="eventType">The type of the event.</param>
    /// <returns>An immutable array of pipelines for the specified event type.</returns>
    public ImmutableArray<IPipeline> Get(Type eventType)
    {
        if(_pipelines.TryGetValue(eventType, out ImmutableArray<IPipeline> pipelines))
        {
            return pipelines;
        }

        return ImmutableArray<IPipeline>.Empty;
    }
}
