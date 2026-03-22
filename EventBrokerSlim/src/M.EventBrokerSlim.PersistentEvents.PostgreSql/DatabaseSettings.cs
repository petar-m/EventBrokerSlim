namespace M.EventBrokerSlim.PersistentEvents.PostgreSql;

/// <summary>
/// Provides configuration settings for connecting to a database, including the connection string and schema name.
/// </summary>
/// <remarks>The default schema name is "ebs_0", which is used for database operations related to the "events"
/// table.</remarks>
public class DatabaseSettings
{
    /// <summary>
    /// Gets or sets the connection string used to establish a connection to the database.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the schema name used for database operations.
    /// </summary>
    /// <remarks>The default value is "ebs_0". This property determines the database schema under which "events" table was created.</remarks>
    public string Schema { get; set; } = "ebs_0";
}
