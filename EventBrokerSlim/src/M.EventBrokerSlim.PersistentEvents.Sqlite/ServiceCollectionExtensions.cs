using System;
using M.EventBrokerSlim.DependencyInjection;
using M.EventBrokerSlim.Persistent;
using M.EventBrokerSlim.PersistentEvents.Sqlite.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace M.EventBrokerSlim.PersistentEvents.Sqlite;

/// <summary>
/// Extensions for configuring SQLite persistence for the event broker.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Configures the event broker to use SQLite for persistent event storage.
    /// </summary>
    /// <param name="builder">The event broker builder.</param>
    /// <param name="configurePersistence">An action to configure the database and broker settings.</param>
    /// <returns>The event broker builder.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the connection string is null.</exception>
    public static EventBrokerBuilder WithSqlitePersistence(this EventBrokerBuilder builder, Action<DatabaseSettings, PersistentEventBrokerSettings> configurePersistence)
    {
        var databaseSettings = new DatabaseSettings();
        var brokerSettings = new PersistentEventBrokerSettings();
        configurePersistence(databaseSettings, brokerSettings);
        if(databaseSettings.ConnectionString is null)
        {
            throw new ArgumentNullException(nameof(databaseSettings.ConnectionString));
        }

        builder.Services
            .AddKeyedSingleton(builder.EventBrokerKey, databaseSettings)
            .AddKeyedSingleton(builder.EventBrokerKey, brokerSettings)
            .AddKeyedSingleton<IEventStorage>(
                builder.EventBrokerKey,
                (x, key) => new SqliteStorage(
                    x.GetRequiredKeyedService<DatabaseSettings>(key),
                    x.GetRequiredKeyedService<PersistentEventBrokerSettings>(key),
                    x.GetService<ILogger<SqliteStorage>>() ?? NullLogger<SqliteStorage>.Instance));

        return builder;
    }
}
