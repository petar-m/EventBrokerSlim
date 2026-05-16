namespace M.EventBrokerSlim.PersistentEvents.Sqlite;

/// <summary>
/// Provides configuration settings for connecting to a SQLite database.
/// </summary>
public class DatabaseSettings
{
    /// <summary>
    /// Gets or sets the connection string used to establish a connection to the SQLite database.
    /// </summary>
    /// <remarks>Example: <c>"Data Source=events.db"</c></remarks>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the name of the database table used for storing events.
    /// </summary>
    /// <remarks>The default value is "events". Use a unique table name per event broker instance when sharing the same database file.</remarks>
    public string Table { get; set; } = "events";
}
