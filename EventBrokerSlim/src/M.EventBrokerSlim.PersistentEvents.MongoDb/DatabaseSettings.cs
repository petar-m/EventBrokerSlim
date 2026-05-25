using MongoDB.Driver;

namespace M.EventBrokerSlim.PersistentEvents.MongoDb;

/// <summary>
/// Provides configuration settings for connecting to a MongoDB database.
/// </summary>
public class DatabaseSettings
{
    /// <summary>
    /// Gets or sets the connection string used to establish a connection to the MongoDB server.
    /// </summary>
    /// <remarks>Required unless <see cref="MongoDatabase"/> is supplied. Example: <c>"mongodb://localhost:27017"</c></remarks>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the name of the MongoDB database used for storing events.
    /// </summary>
    /// <remarks>The default value is <c>"ebs_0"</c>. Ignored when <see cref="MongoDatabase"/> is supplied.</remarks>
    public string DatabaseName { get; set; } = "ebs_0";

    /// <summary>
    /// Gets or sets the name of the MongoDB collection used for storing events.
    /// </summary>
    /// <remarks>The default value is <c>"events"</c>. Use a unique collection name per event broker instance when sharing the same database.</remarks>
    public string CollectionName { get; set; } = "events";

    /// <summary>
    /// Gets or sets an optional existing <see cref="IMongoDatabase"/> instance to use.
    /// </summary>
    /// <remarks>When set, <see cref="ConnectionString"/> and <see cref="DatabaseName"/> are ignored and the provided instance is used as-is.</remarks>
    public IMongoDatabase? MongoDatabase { get; set; }
}
