namespace M.EventBrokerSlim.DependencyInjection;

/// <summary>
/// Options for configuring class-based event handler registrations
/// (<see cref="ServiceCollectionExtensions.AddTransientEventHandler{TEvent, THandler}(Microsoft.Extensions.DependencyInjection.IServiceCollection, System.Action{EventHandlerOptions})"/>,
/// <see cref="ServiceCollectionExtensions.AddScopedEventHandler{TEvent, THandler}(Microsoft.Extensions.DependencyInjection.IServiceCollection, System.Action{EventHandlerOptions})"/>,
/// <see cref="ServiceCollectionExtensions.AddSingletonEventHandler{TEvent, THandler}(Microsoft.Extensions.DependencyInjection.IServiceCollection, System.Action{EventHandlerOptions})"/>).
/// </summary>
public class EventHandlerOptions : HandlerOptionsBase<EventHandlerOptions>
{
    internal EventHandlerOptions() { }

    internal string? ServiceKey { get; private set; }

    /// <summary>
    /// Sets a custom DI service key for the event handler registration.
    /// When not set, the handler is registered as the default (non-keyed) instance.
    /// </summary>
    /// <param name="serviceKey">The DI service key.</param>
    /// <returns>The options instance for fluent chaining.</returns>
    public EventHandlerOptions WithServiceKey(string serviceKey)
    {
        ServiceKey = serviceKey;
        return this;
    }
}
