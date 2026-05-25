using System;
using M.EventBrokerSlim.DependencyInjection;
using M.EventBrokerSlim.Persistent;
using M.EventBrokerSlim.PersistentEvents.MongoDb.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;

namespace M.EventBrokerSlim.PersistentEvents.MongoDb;

/// <summary>
/// Extensions for configuring MongoDB persistence for the event broker.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Configures the event broker to use MongoDB for persistent event storage.
    /// </summary>
    /// <param name="builder">The event broker builder.</param>
    /// <param name="configurePersistence">An action to configure the database and broker settings.</param>
    /// <returns>The event broker builder.</returns>
    /// <exception cref="ArgumentNullException">Thrown when neither a connection string nor a <see cref="IMongoDatabase"/> instance is supplied.</exception>
    public static EventBrokerBuilder WithMongoDbPersistence(this EventBrokerBuilder builder, Action<DatabaseSettings, PersistentEventBrokerSettings> configurePersistence)
    {
        var databaseSettings = new DatabaseSettings();
        var brokerSettings = new PersistentEventBrokerSettings();
        configurePersistence(databaseSettings, brokerSettings);

        if(databaseSettings.MongoDatabase is null && databaseSettings.ConnectionString is null)
        {
            throw new ArgumentNullException(nameof(databaseSettings.ConnectionString), "Either ConnectionString or MongoDatabase must be provided.");
        }

        builder.Services
            .AddKeyedSingleton(builder.EventBrokerKey, databaseSettings)
            .AddKeyedSingleton(builder.EventBrokerKey, brokerSettings)
            .AddKeyedSingleton<MongoClientWrapper>(
                builder.EventBrokerKey,
                (x, key) =>
                {
                    var settings = x.GetRequiredKeyedService<DatabaseSettings>(key);
                    return settings.MongoDatabase is not null
                        ? new MongoClientWrapper(settings.MongoDatabase)
                        : new MongoClientWrapper(settings.ConnectionString!, settings.DatabaseName);
                })
            .AddKeyedSingleton<IEventStorage>(
                builder.EventBrokerKey,
                (x, key) => new MongoDbStorage(
                    x.GetRequiredKeyedService<MongoClientWrapper>(key),
                    x.GetRequiredKeyedService<DatabaseSettings>(key),
                    x.GetRequiredKeyedService<PersistentEventBrokerSettings>(key),
                    x.GetService<ILogger<MongoDbStorage>>() ?? NullLogger<MongoDbStorage>.Instance));

        return builder;
    }
}
