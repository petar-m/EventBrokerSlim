-- This SQL script initializes the database schema for the Event Broker Slim library's persistent event dispatching feature.

-- ----------
-- IMPORTANT: If you have multiple broker instances, ensure a unique schema.table name for each instance.
-- ebs_0  is the default schema name. 
-- events is the default table  name. 
-- ----------
DO $$
DECLARE
    schema  TEXT := 'ebs_0';
    tbl     TEXT := 'events';

    -- Derived names, computed once
    seq                TEXT;
    idx_polling        TEXT;
    idx_timeout        TEXT;
    idx_cleanup        TEXT;
BEGIN
    seq         := schema || '.' || tbl || '_id_seq';
    idx_polling := 'idx_' || schema || '_' || tbl || '_scheduled_polling';
    idx_timeout := 'idx_' || schema || '_' || tbl || '_inprogress_timeout';
    idx_cleanup := 'idx_' || schema || '_' || tbl || '_cleanup';

    EXECUTE format('CREATE SCHEMA IF NOT EXISTS %I', schema);

    EXECUTE format('CREATE SEQUENCE IF NOT EXISTS %s AS BIGINT MINVALUE 1 START 1 INCREMENT 1', seq);

    -- Events table for persistent event dispatch work items
    EXECUTE format('
        CREATE TABLE IF NOT EXISTS %I.%I (
            id                        BIGINT      PRIMARY KEY DEFAULT nextval(%L),
            event_id                  TEXT        NOT NULL,
            event_name                TEXT        NOT NULL,
            handler_name              TEXT        NOT NULL,
            payload                   TEXT        NOT NULL,
            status                    INT         NOT NULL,
            scheduled_at              TIMESTAMPTZ NOT NULL,
            retry_attempt_count       INTEGER     NOT NULL,
            retry_last_delay          INTERVAL    NOT NULL,
            claimed_at                TIMESTAMPTZ NULL,
            created_at                TIMESTAMPTZ NOT NULL,
            last_updated_at           TIMESTAMPTZ NOT NULL,
            last_error                TEXT        NULL,
            processing_timeouts_count INTEGER     NOT NULL
        )', schema, tbl, seq);

    -- 1. Polling Index (Partial Index for Scheduled events)
    -- Query: SELECT ... WHERE status = 1 (Scheduled) AND scheduled_at <= @now ORDER BY scheduled_at ASC
    -- Optimization:
    --   - PARTIAL: Only indexes 'Scheduled' events (status=1). Keeps index small and fast, excluding millions of completed events.
    --   - INCLUDE: Payload-free columns (id, last_updated_at, event_name, handler_name) are included to allow Index-Only Scans.
    --              This avoids visiting the heap for the high-frequency polling query.
    EXECUTE format('
        CREATE INDEX IF NOT EXISTS %I ON %I.%I (scheduled_at ASC)
        INCLUDE (id, last_updated_at, event_name, handler_name)
        WHERE status = 1',
        idx_polling, schema, tbl);

    -- 2. Timeout Index (Partial Index for InProgress events)
    -- Query: UPDATE ... WHERE status = 2 (InProgress) AND claimed_at <= @claimed_before
    -- Optimization:
    --   - PARTIAL: Only indexes 'InProgress' events. Extremely small index.
    EXECUTE format('
        CREATE INDEX IF NOT EXISTS %I ON %I.%I (claimed_at)
        WHERE status = 2',
        idx_timeout, schema, tbl);

    -- 3. Cleanup Index (Partial Index for Terminal states)
    -- Query: DELETE ... WHERE (status = 3 AND ...) OR (status = 4 AND ...)
    -- Optimization:
    --   - PARTIAL: Only indexes 'Completed' (3) and 'DeadLettered' (4) events.
    --   - COMPOSITE: (status, last_updated_at) allows efficient range scans for deletion.
    EXECUTE format('
        CREATE INDEX IF NOT EXISTS %I ON %I.%I (status, last_updated_at)
        WHERE status IN (3, 4)',
        idx_cleanup, schema, tbl);
END;
$$;