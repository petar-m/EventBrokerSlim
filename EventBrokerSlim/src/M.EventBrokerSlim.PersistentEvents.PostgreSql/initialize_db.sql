-- This SQL script initializes the database schema for the Event Broker Slim library's persistent event dispatching feature.

-- ----------
-- IMPORTANT: If you have multiple broker instances, ensure a unique schema.table name for each instance.
-- ebs_0  is the default schema name. 
-- events is the default table  name. 
-- ----------
CREATE SCHEMA IF NOT EXISTS ebs_0;

CREATE SEQUENCE IF NOT EXISTS ebs_0.event_id_seq AS BIGINT MINVALUE 1 START 1 INCREMENT 1;

-- Events table for persistent event dispatch work items
CREATE TABLE IF NOT EXISTS ebs_0.events (
    id BIGINT PRIMARY KEY DEFAULT nextval('ebs_0.event_id_seq'),
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
CREATE INDEX IF NOT EXISTS idx_ebs_0_events_scheduled_polling 
ON ebs_0.events (scheduled_at ASC) 
INCLUDE (id, last_updated_at, event_name, handler_name)
WHERE status = 1;

-- 2. Timeout Index (Partial Index for InProgress events)
-- Query: UPDATE ... WHERE status = 2 (InProgress) AND claimed_at <= @claimed_before
-- Optimization:
--   - PARTIAL: Only indexes 'InProgress' events. Extremely small index.
CREATE INDEX IF NOT EXISTS idx_ebs_0_events_inprogress_timeout 
ON ebs_0.events (claimed_at) 
WHERE status = 2;

-- 3. Cleanup Index (Partial Index for Terminal states)
-- Query: DELETE ... WHERE (status = 3 AND ...) OR (status = 4 AND ...)
-- Optimization:
--   - PARTIAL: Only indexes 'Completed' (3) and 'DeadLettered' (4) events.
--   - COMPOSITE: (status, last_updated_at) allows efficient range scans for deletion.
CREATE INDEX IF NOT EXISTS idx_ebs_0_events_cleanup 
ON ebs_0.events (status, last_updated_at) 
WHERE status IN (3, 4);
