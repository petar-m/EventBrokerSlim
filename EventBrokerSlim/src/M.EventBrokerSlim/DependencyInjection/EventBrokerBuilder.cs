using System;
using Microsoft.Extensions.DependencyInjection;

namespace M.EventBrokerSlim.DependencyInjection;

/// <summary>
/// Registers EventBroker and configures event broker behavior, optionally registers event handlers in DI container.
/// </summary>
public class EventBrokerBuilder
{
    internal EventBrokerBuilder(IServiceCollection services, object eventBrokerKey)
    {
        Services = services;
        EventBrokerKey = eventBrokerKey;
    }

    internal int _maxConcurrentHandlers = 2;

    internal bool _disableMissingHandlerWarningLog;

    /// <summary>
    /// The service collection where event broker services and event handlers are registered. This property is used to add services to the DI container.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// The key used to identify the event broker instance in the DI container.
    /// </summary>
    public object EventBrokerKey { get; }

    /// <summary>
    /// Sets the maximum number of event handlers to run at the same time.
    /// </summary>
    /// <param name="maxConcurrentHandlers">Maximum number of event handlers to run at the same time. Default is 2.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Throws when <paramref name="maxConcurrentHandlers"/> is less than 1.</exception>
    public EventBrokerBuilder WithMaxConcurrentHandlers(int maxConcurrentHandlers)
    {
        if(maxConcurrentHandlers <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxConcurrentHandlers), "MaxConcurrentHandlers should be greater than zero.");
        }

        _maxConcurrentHandlers = maxConcurrentHandlers;
        return this;
    }

    /// <summary>
    /// Turns off Warning log when no handler is found for event. Turned on by default.
    /// </summary>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public EventBrokerBuilder DisableMissingHandlerWarningLog()
    {
        _disableMissingHandlerWarningLog = true;
        return this;
    }
}
