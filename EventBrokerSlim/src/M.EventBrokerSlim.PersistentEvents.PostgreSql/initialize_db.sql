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

-- Useful indexes for claiming and lookups
--CREATE INDEX IF NOT EXISTS idx_ebs_0_events_scheduled_status ON ebs_0.events (scheduled_at, status);
--CREATE INDEX IF NOT EXISTS idx_ebs_0_events_event_id ON ebs_0.events (event_id);
--CREATE INDEX IF NOT EXISTS idx_ebs_0_events_handler_name ON ebs_0.events (handler_name);

