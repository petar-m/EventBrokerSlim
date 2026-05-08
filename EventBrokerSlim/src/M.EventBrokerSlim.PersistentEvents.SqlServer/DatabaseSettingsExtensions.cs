using System;
using System.Data;
using Microsoft.Data.SqlClient;

namespace M.EventBrokerSlim.PersistentEvents.SqlServer;

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
    /// <param name="settings">The database settings containing the connection string used to connect to the target SqlServer database. The
    /// connection string cannot be null or empty.</param>
    public static void CreateEventsTable(this DatabaseSettings settings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.ConnectionString, nameof(settings.ConnectionString));
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.Schema, nameof(settings.Schema));
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.Table, nameof(settings.Table));

        using var connection = new SqlConnection(settings.ConnectionString);
        using var command = connection.CreateCommand();
        command.Parameters.Add(new SqlParameter("@schema", SqlDbType.NVarChar, 128) { Value = settings.Schema });
        command.Parameters.Add(new SqlParameter("@tbl", SqlDbType.NVarChar, 128) { Value = settings.Table });
        command.CommandText = $"""
         -- This SQL script initializes the database schema for the Event Broker Slim library's persistent event dispatching feature.

         -- ----------
         -- IMPORTANT: If you have multiple broker instances, ensure a unique schema.table name for each instance.
         -- ebs_0  is the default schema name.
         -- events is the default table  name.
         -- ----------

         DECLARE
             -- @schema         NVARCHAR(128) = N'ebs_0',
             -- @tbl            NVARCHAR(128) = N'events',

             -- Derived names, computed once
             @idx_polling    NVARCHAR(256),
             @idx_timeout    NVARCHAR(256),
             @idx_cleanup    NVARCHAR(256),
             @sql            NVARCHAR(MAX),
             @params         NVARCHAR(MAX);

         SET @idx_polling = N'idx_' + @schema + N'_' + @tbl + N'_scheduled_polling';
         SET @idx_timeout = N'idx_' + @schema + N'_' + @tbl + N'_inprogress_timeout';
         SET @idx_cleanup = N'idx_' + @schema + N'_' + @tbl + N'_cleanup';

         -- Create schema if it doesn't exist
         SET @params = N'@schema NVARCHAR(128)';
         SET @sql    = N'
         IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = @schema)
             EXEC(''CREATE SCHEMA ' + QUOTENAME(@schema) + N''')';
         EXEC sp_executesql @sql, @params, @schema = @schema;

         -- Events table
         SET @params = N'@schema NVARCHAR(128), @tbl NVARCHAR(128)';
         SET @sql    = N'
         IF NOT EXISTS (
             SELECT 1
             FROM   sys.tables  t
             JOIN   sys.schemas s ON s.schema_id = t.schema_id
             WHERE  s.name = @schema
             AND    t.name = @tbl
         )
         BEGIN
             CREATE TABLE ' + QUOTENAME(@schema) + N'.' + QUOTENAME(@tbl) + N' (
                 id                        BIGINT            NOT NULL IDENTITY(1,1)  PRIMARY KEY,
                 event_id                  NVARCHAR(128)     NOT NULL,
                 event_name                NVARCHAR(128)     NOT NULL,
                 handler_name              NVARCHAR(128)     NOT NULL,
                 payload                   NVARCHAR(MAX)     NOT NULL,
                 status                    INT               NOT NULL,
                 scheduled_at              DATETIMEOFFSET    NOT NULL,
                 retry_attempt_count       INT               NOT NULL,
                 retry_last_delay          BIGINT            NOT NULL,  -- milliseconds (no native INTERVAL in SQL Server)
                 claimed_at                DATETIMEOFFSET        NULL,
                 created_at                DATETIMEOFFSET    NOT NULL,
                 last_updated_at           DATETIMEOFFSET    NOT NULL,
                 last_error                NVARCHAR(MAX)         NULL,
                 processing_timeouts_count INT               NOT NULL
             )
         END';
         EXEC sp_executesql @sql, @params, @schema = @schema, @tbl = @tbl;

         -- 1. Polling Index (Filtered Index for Scheduled events)
         -- Query: SELECT ... WHERE status = 1 (Scheduled) AND scheduled_at <= @now ORDER BY scheduled_at ASC
         -- Optimization:
         --   - FILTERED: Only indexes 'Scheduled' events (status=1). Keeps index small and fast.
         --   - INCLUDE:  Payload-free columns allow covering index scans, avoiding key lookups.
         SET @params = N'@schema NVARCHAR(128), @tbl NVARCHAR(128), @idx NVARCHAR(256)';
         SET @sql    = N'
         IF NOT EXISTS (
             SELECT 1
             FROM   sys.indexes i
             JOIN   sys.tables  t ON t.object_id  = i.object_id
             JOIN   sys.schemas s ON s.schema_id  = t.schema_id
             WHERE  i.name   = @idx
             AND    t.name   = @tbl
             AND    s.name   = @schema
         )
         BEGIN
             CREATE INDEX ' + QUOTENAME(@idx_polling) + N'
                 ON ' + QUOTENAME(@schema) + N'.' + QUOTENAME(@tbl) + N' (scheduled_at ASC)
                 INCLUDE (id, last_updated_at, event_name, handler_name)
                 WHERE status = 1
         END';
         EXEC sp_executesql @sql, @params, @schema = @schema, @tbl = @tbl, @idx = @idx_polling;

         -- 2. Timeout Index (Filtered Index for InProgress events)
         -- Query: UPDATE ... WHERE status = 2 (InProgress) AND claimed_at <= @claimed_before
         -- Optimization:
         --   - FILTERED: Only indexes 'InProgress' events. Extremely small index.
         SET @sql = N'
         IF NOT EXISTS (
             SELECT 1
             FROM   sys.indexes i
             JOIN   sys.tables  t ON t.object_id  = i.object_id
             JOIN   sys.schemas s ON s.schema_id  = t.schema_id
             WHERE  i.name   = @idx
             AND    t.name   = @tbl
             AND    s.name   = @schema
         )
         BEGIN
             CREATE INDEX ' + QUOTENAME(@idx_timeout) + N'
                 ON ' + QUOTENAME(@schema) + N'.' + QUOTENAME(@tbl) + N' (claimed_at)
                 WHERE status = 2
         END';
         EXEC sp_executesql @sql, @params, @schema = @schema, @tbl = @tbl, @idx = @idx_timeout;

         -- 3. Cleanup Index (Filtered Index for Terminal states)
         -- Query: DELETE ... WHERE (status = 3 AND ...) OR (status = 4 AND ...)
         -- Optimization:
         --   - FILTERED: Only indexes 'Completed' (3) and 'DeadLettered' (4) events.
         --   - COMPOSITE: (status, last_updated_at) allows efficient range scans for deletion.
         SET @sql = N'
         IF NOT EXISTS (
             SELECT 1
             FROM   sys.indexes i
             JOIN   sys.tables  t ON t.object_id  = i.object_id
             JOIN   sys.schemas s ON s.schema_id  = t.schema_id
             WHERE  i.name   = @idx
             AND    t.name   = @tbl
             AND    s.name   = @schema
         )
         BEGIN
             CREATE INDEX ' + QUOTENAME(@idx_cleanup) + N'
                 ON ' + QUOTENAME(@schema) + N'.' + QUOTENAME(@tbl) + N' (status, last_updated_at)
                 WHERE status IN (3, 4)
         END';
         EXEC sp_executesql @sql, @params, @schema = @schema, @tbl = @tbl, @idx = @idx_cleanup;        
         """;
        connection.Open();
        command.ExecuteNonQuery();
    }
}
