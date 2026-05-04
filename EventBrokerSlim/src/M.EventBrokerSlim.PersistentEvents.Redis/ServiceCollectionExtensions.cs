using System;
using M.EventBrokerSlim.DependencyInjection;
using M.EventBrokerSlim.Persistent;
using M.EventBrokerSlim.PersistentEvents.Redis.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;

namespace M.EventBrokerSlim.PersistentEvents.Redis;

/// <summary>
/// Extensions for configuring Redis persistence for the event broker.
/// This class provides a method to register the necessary services for using Redis as the storage mechanism for persistent events in the event broker.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Configures the event broker to use Redis for persistent event storage.
    /// </summary>
    /// <param name="builder">The event broker builder.</param>
    /// <param name="configurePersistence">An action to configure the database and broker settings.</param>
    /// <returns>The event broker builder.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the connection string is null and no registered multiplexer is used.</exception>
    public static EventBrokerBuilder WithRedisPersistence(this EventBrokerBuilder builder, Action<RedisSettings, PersistentEventBrokerSettings> configurePersistence)
    {
        var redisSettings = new RedisSettings();
        var brokerSettings = new PersistentEventBrokerSettings();
        configurePersistence(redisSettings, brokerSettings);
        if(!redisSettings.UseRegisteredMultiplexer && redisSettings.ConnectionString is null)
        {
            throw new ArgumentNullException(nameof(redisSettings.ConnectionString));
        }

        builder.Services
            .AddKeyedSingleton(builder.EventBrokerKey, redisSettings)
            .AddKeyedSingleton(builder.EventBrokerKey, brokerSettings)
            .AddKeyedSingleton<IEventStorage>(
                builder.EventBrokerKey,
                (x, key) => new RedisStorage(
                    x.GetRequiredKeyedService<RedisSettings>(key),
                    x.GetRequiredKeyedService<PersistentEventBrokerSettings>(key),
                    redisSettings.UseRegisteredMultiplexer 
                        ? x.GetService<IConnectionMultiplexer>() ?? throw new InvalidOperationException("IConnectionMultiplexer is not registered.")
                        : ConnectionMultiplexer.Connect(redisSettings.ConnectionString!),
                    x.GetService<ILogger<RedisStorage>>() ?? NullLogger<RedisStorage>.Instance));

        return builder;
    }
}
