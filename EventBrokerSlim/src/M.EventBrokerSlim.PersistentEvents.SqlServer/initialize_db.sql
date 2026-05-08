-- This SQL script initializes the database schema for the Event Broker Slim library's persistent event dispatching feature.

-- ----------
-- IMPORTANT: If you have multiple broker instances, ensure a unique schema.table name for each instance.
-- ebs_0  is the default schema name.
-- events is the default table  name.
-- ----------

DECLARE
    @schema         NVARCHAR(128) = N'ebs_0',
    @tbl            NVARCHAR(128) = N'events',

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
        claimed_at                DATETIMEOFFSET    NULL,
        created_at                DATETIMEOFFSET    NOT NULL,
        last_updated_at           DATETIMEOFFSET    NOT NULL,
        last_error                NVARCHAR(MAX)     NULL,
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