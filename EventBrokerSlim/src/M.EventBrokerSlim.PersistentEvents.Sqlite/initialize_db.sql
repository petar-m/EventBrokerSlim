-- This SQL script initializes the database schema for the Event Broker Slim library's persistent event dispatching feature.

-- ----------
-- IMPORTANT: If you have multiple broker instances sharing the same database file, use a unique table name for each.
-- events is the default table name.
-- ----------

PRAGMA journal_mode=WAL;

CREATE TABLE IF NOT EXISTS events (
    id                        INTEGER PRIMARY KEY AUTOINCREMENT,
    event_id                  TEXT    NOT NULL,
    event_name                TEXT    NOT NULL,
    handler_name              TEXT    NOT NULL,
    payload                   TEXT    NOT NULL,
    status                    INTEGER NOT NULL,
    scheduled_at              TEXT    NOT NULL,  -- ISO 8601 UTC (e.g. 2024-01-15T12:30:00.0000000Z)
    retry_attempt_count       INTEGER NOT NULL,
    retry_last_delay          INTEGER NOT NULL,  -- milliseconds
    claimed_at                TEXT    NULL,       -- ISO 8601 UTC
    created_at                TEXT    NOT NULL,   -- ISO 8601 UTC
    last_updated_at           TEXT    NOT NULL,   -- ISO 8601 UTC (used for optimistic concurrency)
    last_error                TEXT    NULL,
    processing_timeouts_count INTEGER NOT NULL
);

-- 1. Polling Index (Partial Index for Scheduled events)
-- Query: SELECT ... WHERE status = 1 (Scheduled) AND scheduled_at <= @now ORDER BY scheduled_at ASC
-- Optimization:
--   - PARTIAL: Only indexes 'Scheduled' events (status=1). Keeps index small and fast.
CREATE INDEX IF NOT EXISTS idx_events_scheduled_polling
ON events (scheduled_at ASC)
WHERE status = 1;

-- 2. Timeout Index (Partial Index for InProgress events)
-- Query: UPDATE ... WHERE status = 2 (InProgress) AND claimed_at <= @claimed_before
-- Optimization:
--   - PARTIAL: Only indexes 'InProgress' events. Extremely small index.
CREATE INDEX IF NOT EXISTS idx_events_inprogress_timeout
ON events (claimed_at)
WHERE status = 2;

-- 3. Cleanup Index (Partial Index for Terminal states)
-- Query: DELETE ... WHERE (status = 3 AND ...) OR (status = 4 AND ...)
-- Optimization:
--   - PARTIAL: Only indexes 'Completed' (3) and 'DeadLettered' (4) events.
--   - COMPOSITE: (status, last_updated_at) allows efficient range scans for deletion.
CREATE INDEX IF NOT EXISTS idx_events_cleanup
ON events (status, last_updated_at)
WHERE status IN (3, 4);
