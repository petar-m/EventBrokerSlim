namespace M.EventBrokerSlim.PersistentEvents.Redis;

/// <summary>
/// Provides configuration settings for connecting to a Redis server, including the connection string and key prefix.
/// </summary>
/// <remarks>The default key prefix is "ebs_0", which is used for Redis operations related to the "events".</remarks>
public class RedisSettings
{
    /// <summary>
    /// Gets or sets the connection string used to establish a connection to the Redis server.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// If set to true, the event broker will use a registered ConnectionMultiplexer instance from the dependency injection container instead of creating a new one using the provided connection string. The ConnectionMultiplexer instance should be registered with the same key as the event broker. The default value is false.
    /// </summary>
    public bool UseRegisteredMultiplexer { get; set; } = false;

    /// <summary>
    /// Gets or sets the key prefix used for Redis operations.
    /// </summary>
    /// <remarks>The default value is "ebs_0". This property determines the Redis key prefix under which "events" table was created.</remarks>
    public string KeyPrefix { get; set; } = "ebs_0";
}
