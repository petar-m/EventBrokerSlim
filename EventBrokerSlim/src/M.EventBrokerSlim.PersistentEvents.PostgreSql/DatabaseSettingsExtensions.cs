using System;
using Npgsql;

namespace M.EventBrokerSlim.PersistentEvents.PostgreSql;

/// <summary>
/// Represents extension methods for the <see cref="DatabaseSettings"/> class.
/// </summary>
public static class DatabaseSettingsExtensions
{
    /// <summary>
    /// Creates the events table and related database schema required for persistent event dispatching in the Event
    /// Broker Slim library if they do not already exist.
    /// </summary>
    /// <remarks>
    /// <b>
    /// NOTE: This method require DML permissions on the database to create schemas, tables, sequences, and indexes.
    /// Production applications should typically run this initialization step as part of a deployment or migration process, rather than at application startup, to ensure proper permissions and avoid potential performance impacts.
    /// </b>
    /// <para>
    /// This method initializes the default schema and indexes necessary for event persistence and
    /// dispatching. If the schema or tables already exist, the operation is idempotent and will not overwrite existing
    /// data. The default schema name is 'ebs_0'; if multiple instances are used, ensure each uses a unique schema name
    /// to avoid conflicts.
    /// </para>
    /// </remarks>
    /// <param name="settings">The database settings containing the connection string used to connect to the target PostgreSQL database. The
    /// connection string cannot be null or empty.</param>
    public static void CreateEventsTable(this DatabaseSettings settings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.ConnectionString, nameof(settings.ConnectionString));
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.Schema, nameof(settings.Schema));
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.Table, nameof(settings.Table));

        using var dataSource = NpgsqlDataSource.Create(settings.ConnectionString);
        using var connection = dataSource.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
         -- This SQL script initializes the database schema for the Event Broker Slim library's persistent event dispatching feature.

         -- ----------
         -- IMPORTANT: If you have multiple broker instances, ensure a unique schema.table name for each instance.
         -- ebs_0  is the default schema name. 
         -- events is the default table  name. 
         -- ----------
         CREATE SCHEMA IF NOT EXISTS {settings.Schema};

         CREATE SEQUENCE IF NOT EXISTS {settings.Schema}.{settings.Table}_id_seq AS BIGINT MINVALUE 1 START 1 INCREMENT 1;

         -- Events table for persistent event dispatch work items
         CREATE TABLE IF NOT EXISTS {settings.Schema}.{settings.Table} (
             id BIGINT PRIMARY KEY DEFAULT nextval('{settings.Schema}.{settings.Table}_id_seq'),
             event_id TEXT NOT NULL,
             event_name TEXT NOT NULL,
             handler_name TEXT NOT NULL,
             payload TEXT NOT NULL,
             status INT NOT NULL,
             scheduled_at TIMESTAMP WITH TIME ZONE NOT NULL,
             retry_attempt_count INTEGER NOT NULL,
             retry_last_delay INTERVAL NOT NULL,
             claimed_at TIMESTAMP WITH TIME ZONE NULL,
             created_at TIMESTAMP WITH TIME ZONE NOT NULL,
             last_updated_at TIMESTAMP WITH TIME ZONE NOT NULL,
             last_error TEXT NULL,
             processing_timeouts_count INTEGER NOT NULL
         );

         -- 1. Polling Index (Partial Index for Scheduled events)
         -- Query: SELECT ... WHERE status = 1 (Scheduled) AND scheduled_at <= @now ORDER BY scheduled_at ASC
         -- Optimization: 
         --   - PARTIAL: Only indexes 'Scheduled' events (status=1). Keeps index small and fast, excluding millions of completed events.
         --   - INCLUDE: Payload-free columns (id, last_updated_at, event_name, handler_name) are included to allow Index-Only Scans.
         --              This avoids visiting the heap for the high-frequency polling query.
         CREATE INDEX IF NOT EXISTS idx_{settings.Schema}_{settings.Table}_scheduled_polling 
         ON {settings.Schema}.{settings.Table} (scheduled_at ASC) 
         INCLUDE (id, last_updated_at, event_name, handler_name)
         WHERE status = 1;

         -- 2. Timeout Index (Partial Index for InProgress events)
         -- Query: UPDATE ... WHERE status = 2 (InProgress) AND claimed_at <= @claimed_before
         -- Optimization:
         --   - PARTIAL: Only indexes 'InProgress' events. Extremely small index.
         CREATE INDEX IF NOT EXISTS idx_{settings.Schema}_{settings.Table}_inprogress_timeout 
         ON {settings.Schema}.{settings.Table} (claimed_at) 
         WHERE status = 2;

         -- 3. Cleanup Index (Partial Index for Terminal states)
         -- Query: DELETE ... WHERE (status = 3 AND ...) OR (status = 4 AND ...)
         -- Optimization:
         --   - PARTIAL: Only indexes 'Completed' (3) and 'DeadLettered' (4) events.
         --   - COMPOSITE: (status, last_updated_at) allows efficient range scans for deletion.
         CREATE INDEX IF NOT EXISTS idx_{settings.Schema}_{settings.Table}_cleanup 
         ON {settings.Schema}.{settings.Table} (status, last_updated_at) 
         WHERE status IN (3, 4);
         """;
        connection.Open();
        command.ExecuteNonQuery();
    }
}
