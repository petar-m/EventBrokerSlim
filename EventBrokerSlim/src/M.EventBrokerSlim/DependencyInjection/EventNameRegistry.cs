using System;
using System.Collections.Generic;

namespace M.EventBrokerSlim.DependencyInjection;

/// <summary>
/// Provides a registry for associating event names with their corresponding event types.
/// </summary>
/// <remarks>Use this class to map string-based event names to specific event types, enabling type-safe event
/// handling and lookup by name.</remarks>
public class EventNameRegistry
{
    private readonly Dictionary<string, Type> _eventNames = new();

    /// <summary>
    /// Registers an event type with the specified name in the registry.
    /// </summary>
    /// <typeparam name="TEvent">The type of the event to associate with the specified name.</typeparam>
    /// <param name="name">The name to associate with the event type. Event name must be unique. Cannot be null or empty.</param>
    /// <returns>The current instance of <see cref="EventNameRegistry"/> to allow method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown if the provided name is null or empty, or if an event with the same name"</exception>
    public EventNameRegistry Add<TEvent>(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Type eventType = typeof(TEvent);
        if(_eventNames.TryGetValue(name, out Type? t))
        {
            if(t == eventType)
            {
                return this;
            }

            throw new ArgumentException($"An event with the name '{name}' is already registered with type '{t.FullName}'.");
        }

        _eventNames[name] = eventType;
        return this;
    }

    /// <summary>
    /// Retrieves the .NET type associated with the specified event name, if it exists.
    /// </summary>
    /// <param name="name">The name of the event for which to retrieve the associated type. Cannot be null.</param>
    /// <returns>The <see cref="Type"/> of the event if found; otherwise, <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the provided name is null.</exception>"
    public Type? GetEventType(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        _ = _eventNames.TryGetValue(name, out Type? eventType);
        return eventType;
    }
}
