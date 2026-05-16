using System;
using Microsoft.Data.Sqlite;

namespace M.EventBrokerSlim.PersistentEvents.Sqlite;

/// <summary>
/// Represents extension methods for the <see cref="DatabaseSettings"/> class.
/// </summary>
public static class DatabaseSettingsExtensions
{
    /// <summary>
    /// Creates the events table and related indexes required for persistent event dispatching if they do not already exist.
    /// </summary>
    /// <remarks>
    /// <b>
    /// NOTE: This method requires DDL permissions on the database to create tables and indexes.
    /// Production applications should typically run this initialization step as part of a deployment or migration process,
    /// rather than at application startup, to ensure proper permissions and avoid potential performance impacts.
    /// </b>
    /// <para>
    /// This method also enables WAL (Write-Ahead Logging) journal mode, which improves concurrent read/write performance.
    /// The operation is idempotent — it uses <c>CREATE TABLE IF NOT EXISTS</c> and is safe to run on every startup in development.
    /// </para>
    /// <para>
    /// Use a unique <see cref="DatabaseSettings.Table"/> name per event broker instance when sharing the same database file.
    /// </para>
    /// </remarks>
    /// <param name="settings">The database settings containing the connection string and table name.</param>
    public static void CreateEventsTable(this DatabaseSettings settings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.ConnectionString, nameof(settings.ConnectionString));
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.Table, nameof(settings.Table));

        using var connection = new SqliteConnection(settings.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            PRAGMA journal_mode=WAL;

            CREATE TABLE IF NOT EXISTS {settings.Table} (
                id                        INTEGER PRIMARY KEY AUTOINCREMENT,
                event_id                  TEXT    NOT NULL,
                event_name                TEXT    NOT NULL,
                handler_name              TEXT    NOT NULL,
                payload                   TEXT    NOT NULL,
                status                    INTEGER NOT NULL,
                scheduled_at              TEXT    NOT NULL,
                retry_attempt_count       INTEGER NOT NULL,
                retry_last_delay          INTEGER NOT NULL,
                claimed_at                TEXT    NULL,
                created_at                TEXT    NOT NULL,
                last_updated_at           TEXT    NOT NULL,
                last_error                TEXT    NULL,
                processing_timeouts_count INTEGER NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_{settings.Table}_scheduled_polling
            ON {settings.Table} (scheduled_at ASC)
            WHERE status = 1;

            CREATE INDEX IF NOT EXISTS idx_{settings.Table}_inprogress_timeout
            ON {settings.Table} (claimed_at)
            WHERE status = 2;

            CREATE INDEX IF NOT EXISTS idx_{settings.Table}_cleanup
            ON {settings.Table} (status, last_updated_at)
            WHERE status IN (3, 4);
            """;
        command.ExecuteNonQuery();
    }
}
