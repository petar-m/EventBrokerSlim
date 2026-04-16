using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using M.EventBrokerSlim.Persistent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace M.EventBrokerSlim.DependencyInjection;

/// <summary>
/// Represents a registry for managing event pipelines.
/// </summary>
public class PipelineRegistry
{
    private readonly FrozenDictionary<Type, ImmutableArray<EventPipeline>> _pipelines;
    private readonly FrozenDictionary<string, EventPipeline> _namedHandlers;
    private readonly FrozenDictionary<Type, ImmutableArray<string>> _handlerNamesByEventType;

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineRegistry"/> class.
    /// </summary>
    /// <param name="pipelines">The collection of event pipelines to register.</param>
    /// <param name="serviceScopeFactory">The optional service scope factory for pipeline services.</param>
    public PipelineRegistry(IEnumerable<EventPipeline> pipelines, IServiceScopeFactory serviceScopeFactory)
    {
        _pipelines = pipelines
            .Where(x => x.Pipeline is not NullPipeline)
            .Select(x =>
            {
                x.Pipeline.ServiceScopeFactory ??= serviceScopeFactory;
                return x;
            })
            .GroupBy(x => x.Event)
            .ToFrozenDictionary(
                x => x.Key,
                x => x.ToImmutableArray());

        _handlerNamesByEventType = pipelines
             .Where(x => !string.IsNullOrEmpty(x.HandlerName))
             .GroupBy(x => x.Event)
             .ToFrozenDictionary(
                x => x.Key,
                x => x.Select(x => x.HandlerName!).ToImmutableArray());

        _namedHandlers = pipelines
             .Where(x => !string.IsNullOrEmpty(x.HandlerName) && x.Pipeline is not NullPipeline)
             .GroupBy(x => x.HandlerName)
             .ToFrozenDictionary(x => x.Key!, x => x.Single());
    }

    /// <summary>
    /// Retrieves the pipelines associated with the specified event type.
    /// </summary>
    /// <param name="eventType">The type of the event.</param>
    /// <returns>An immutable array of pipelines for the specified event type.</returns>
    public ImmutableArray<EventPipeline> Get(Type eventType)
    {
        if(_pipelines.TryGetValue(eventType, out ImmutableArray<EventPipeline> pipelines))
        {
            return pipelines;
        }

        return ImmutableArray<EventPipeline>.Empty;
    }

    /// <summary>
    /// Retrieves the event pipeline associated with the specified name.
    /// </summary>
    /// <param name="name">The name of the event pipeline to retrieve. Cannot be null or empty.</param>
    /// <returns>The <see cref="EventPipeline"/> associated with the specified name, or <see langword="null"/> if no pipeline
    /// exists for that name.</returns>
    /// <exception cref="ArgumentException">Thrown when the <paramref name="name"/> is null or empty.</exception>"
    public EventPipeline? Get(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        _ = _namedHandlers.TryGetValue(name, out var pipeline);
        return pipeline;
    }

    /// <summary>
    /// Retrieves the names of the handlers associated with the specified event type.
    /// </summary>
    /// <remarks>This method uses the event type to look up the corresponding handler names. If no handlers
    /// are found, an empty array is returned.</remarks>
    /// <typeparam name="TEvent">The type of the event for which handler names are being retrieved.</typeparam>
    /// <returns>An immutable array of strings containing the names of the handlers for the specified event type. The array will
    /// be empty if no handlers are registered for the event type.</returns>
    public ImmutableArray<string> GetHandlerNames<TEvent>()
    {
        return _handlerNamesByEventType.TryGetValue(typeof(TEvent), out var handlerNames) 
            ? handlerNames 
            : ImmutableArray<string>.Empty;
    }

    /// <summary>
    /// Validates the persistent event configuration against the specified event registry.
    /// </summary>
    /// <param name="eventRegistry">The event registry to validate against.</param>
    /// <param name="logger">Optional logger for emitting warnings.</param>
    /// <param name="throwOnErrors">When <see langword="true"/>, throws <see cref="InvalidOperationException"/> on validation errors instead of logging warnings.</param>
    internal void Validate(EventRegistry eventRegistry, ILogger? logger, bool throwOnErrors)
    {
        // Rule 1: every handler with a handlerName must have its event type in EventRegistry
        foreach(var kvp in _handlerNamesByEventType)
        {
            Type eventType = kvp.Key;
            if(!eventRegistry.HasEventType(eventType))
            {
                string message = $"Handler(s) [{string.Join(", ", kvp.Value)}] for event type '{eventType.FullName}' have a handlerName but the event type is not registered in EventRegistry. " +
                    "Register it with EventRegistry.Add<TEvent>(name) to enable persistence.";
                if(throwOnErrors)
                {
                    throw new InvalidOperationException(message);
                }

                logger?.LogWarning(message);
            }
        }

        // Rule 2: every event type in EventRegistry should have at least one handler with a handlerName — warn if not
        foreach(Type eventType in eventRegistry.GetRegisteredEventTypes())
        {
            if(!_handlerNamesByEventType.ContainsKey(eventType))
            {
                string message = $"Event type '{eventType.FullName}' is registered in EventRegistry but no handler with a handlerName is registered for it. " +
                    "Events of this type will be written to the store but never claimed.";
                if(throwOnErrors)
                {
                    throw new InvalidOperationException(message);
                }

                logger?.LogWarning(message);
            }
        }
    }
}
