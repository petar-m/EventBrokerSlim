using System;
using System.Collections.Generic;

namespace M.EventBrokerSlim.Persistent;

/// <summary>
/// Maintains a bidirectional registry between string event names and event types.
/// </summary>
/// <remarks>
/// Use this class to map a unique string name to a specific event <see cref="Type"/> and to
/// lookup either the <see cref="Type"/> by name or the registered name by type.
/// The registry enforces uniqueness of names and of event types: a name can point to only one
/// event type and a type can be registered under only one name.
/// </remarks>
public class EventRegistry
{
    private readonly Dictionary<string, Type> _eventNameTypeMap;
    private readonly Dictionary<Type, string> _eventTypeNameMap;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventRegistry"/> class.
    /// </summary>
    public EventRegistry()
    {
        _eventNameTypeMap = new Dictionary<string, Type>();
        _eventTypeNameMap = new Dictionary<Type, string>();
    }

    /// <summary>
    /// Registers an event type with the specified unique name in the registry.
    /// </summary>
    /// <typeparam name="TEvent">The event CLR type to associate with <paramref name="name"/>.</typeparam>
    /// <param name="name">The unique string name for the event. Cannot be null or empty.</param>
    /// <returns>The current <see cref="EventRegistry"/> instance to allow method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or empty, when the name is already registered for a different type, or when the type is already registered under a different name.</exception>
    public EventRegistry Add<TEvent>(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Type eventType = typeof(TEvent);

        if(_eventNameTypeMap.TryGetValue(name, out Type? t) && t == eventType)
        {
            return this;
        }

        if(t is not null)
        {
            throw new InvalidOperationException($"Can't register event with type '{eventType.FullName}'. A registry entry for name '{name}' already exists: ({name}, {t.FullName}).");
        }

        if(_eventTypeNameMap.TryGetValue(eventType, out string? n))
        {
            throw new InvalidOperationException($"Can't register event with name '{name}'. A registry entry for type '{eventType.FullName}' already exists: ({n}, {eventType.FullName}).");
        }

        _eventNameTypeMap[name] = eventType;
        _eventTypeNameMap[eventType] = name;
        return this;
    }

    /// <summary>
    /// Retrieves the <see cref="Type"/> associated with the specified event name.
    /// </summary>
    /// <param name="name">The event name to look up. Cannot be null.</param>
    /// <returns>The registered <see cref="Type"/> if the name exists in the registry; otherwise <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="name"/> is null.</exception>
    public Type? GetEventType(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        _ = _eventNameTypeMap.TryGetValue(name, out Type? eventType);
        return eventType;
    }

    /// <summary>
    /// Gets the registered event name for the given event CLR type.
    /// </summary>
    /// <typeparam name="TEvent">The event CLR type to lookup.</typeparam>
    /// <returns>The registered event name if the type is present in the registry; otherwise <see langword="null"/>.</returns>
    public string? GetEventName<TEvent>()
    {
        Type eventType = typeof(TEvent);
        _ = _eventTypeNameMap.TryGetValue(eventType, out string? eventName);
        return eventName;
    }

    /// <summary>
    /// Checks whether the specified event type is registered in the registry.
    /// </summary>
    internal bool HasEventType(Type eventType) => _eventTypeNameMap.ContainsKey(eventType);

    /// <summary>
    /// Gets all event types registered in the registry.
    /// </summary>
    internal IEnumerable<Type> GetRegisteredEventTypes() => _eventTypeNameMap.Keys;
}
