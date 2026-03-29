namespace M.EventBrokerSlim.DependencyInjection;

/// <summary>
/// Base class for handler registration options, providing common configuration shared by both
/// class-based event handlers and pipeline handlers.
/// </summary>
/// <typeparam name="TSelf">The concrete derived options type (CRTP pattern for fluent API).</typeparam>
public abstract class HandlerOptionsBase<TSelf> where TSelf : HandlerOptionsBase<TSelf>
{
    internal HandlerOptionsBase() { }

    internal object? EventBrokerKey { get; private set; }

    internal string? HandlerName { get; private set; }

    /// <summary>
    /// Associates the handler with a specific keyed event broker instance.
    /// </summary>
    /// <param name="eventBrokerKey">The key identifying the event broker instance.</param>
    /// <returns>The options instance for fluent chaining.</returns>
    public TSelf ForBroker(object eventBrokerKey)
    {
        EventBrokerKey = eventBrokerKey;
        return (TSelf)this;
    }

    /// <summary>
    /// Sets a unique name for the handler. Mandatory for persistent events.
    /// </summary>
    /// <param name="handlerName">The unique handler name.</param>
    /// <returns>The options instance for fluent chaining.</returns>
    public TSelf WithHandlerName(string handlerName)
    {
        HandlerName = handlerName;
        return (TSelf)this;
    }
}
