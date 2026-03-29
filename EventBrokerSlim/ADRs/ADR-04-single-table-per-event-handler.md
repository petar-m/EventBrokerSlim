# ADR-04: One store table, one record per (event, handler name)

**Status:** Accepted  
**Date:** 2026-03-26

## Context

The persistence layer needs a schema for storing event records. Fan-out at write time ([ADR-01](ADR-01-fan-out-at-write-time.md)) produces one record per handler for each published event — this section decides where those records live. Two approaches were considered:

- **Single table** — all records live in one table, with the event payload duplicated across handler records for the same event.
- **Two tables** — one event row plus multiple handler rows, eliminating payload duplication but adding a join on every poll query and complicating cleanup (the event row can only be deleted once all its handler rows are in a terminal state).

## Decision

All records — regardless of event type or handler — live in a single table. The record schema includes the event name, handler name, serialized payload, status, attempt count, retry timestamp, claim metadata, and error information. The event payload is duplicated across all handler records for the same event.

## Consequences

- The schema is simple and backend-agnostic.
- Querying by handler name and status is sufficient for all polling operations.
- Each record is fully self-contained — claiming, dispatching, retrying, and cleaning up a record requires no joins and no coordination with other records.
- The payload duplication is the accepted tradeoff for the simplicity gains.
