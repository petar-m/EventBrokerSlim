using LiteDB;

namespace M.EventBrokerSlim.PersistentEvents.LiteDb;

/// <summary>
/// Provides configuration settings for connecting to a LiteDB database.
/// </summary>
public class DatabaseSettings
{
    /// <summary>
    /// Gets or sets the connection string used to establish a connection to the LiteDB database.
    /// </summary>
    /// <remarks>Example: <c>"Filename=events.db"</c></remarks>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the name of the LiteDB collection used for storing events.
    /// </summary>
    /// <remarks>The default value is "events". Use a unique collection name per event broker instance when sharing the same database file.</remarks>
    public string Collection { get; set; } = "events";

    /// <summary>
    /// Gets or sets an optional instance of <see cref="LiteDatabase"/> to be used by the event storage implementation.
    /// </summary>
    public LiteDatabase? LiteDbInstance { get; set; }
}
