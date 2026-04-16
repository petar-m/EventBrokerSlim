# ADR-03: Optimistic concurrency claiming with broad polling

**Status:** Accepted  
**Date:** 2026-03-26

## Context

With multiple instances competing for the same records, a claiming mechanism is needed to ensure at most one instance processes any given record under normal operation. The mechanism must work uniformly across diverse backends (SQL databases, MongoDB, Redis, CosmosDB) without requiring distributed locks or consensus protocols.

Two query strategies were considered for fetching claimable records:

- **Filtered query** — include a handler name filter so each instance only fetches records for its own handlers.
- **Broad query** — fetch all `Scheduled` records regardless of handler name and filter in memory.

## Decision

The polling service fetches a batch of candidate records using a broad query — `status = 'Scheduled' AND scheduled_at <= now` — with no handler name filter and passes them to the handler runner via an internal channel. The handler runner discards candidates with no matching local handler.

When a concurrency slot is available, the handler runner attempts to claim a candidate using optimistic concurrency: a conditional update that sets `status = InProgress` and `claimed_at = now` only if `status` is still `Scheduled` at update time. If another instance claimed the record first, the condition fails and the handler moves on. The claim attempt happens immediately before processing, not during polling.

The mechanism varies by backend — a conditional `UPDATE` with row count check for SQL, `findOneAndUpdate` for MongoDB, an atomic Lua script for Redis, a conditional patch with ETag for CosmosDB — but the outcome is identical across all backends: at most one instance claims any given record.

No instance identifier is stored — with horizontal scaling there is no reliable stable identifier per instance, and none is needed.

Recovery of records claimed by crashed instances is handled by periodic maintenance loops — see [ADR-10](ADR-10-maintenance-and-recovery.md).

## Consequences

- A broad query is simpler and more efficiently indexable than a query filtered by a potentially large list of handler names.
- In-memory filtering before claiming avoids wasteful claim-then-release cycles in partial deployments.
- Optimistic concurrency keeps the claim mechanism uniform across backends — each backend expresses the same conditional update in its own idiom without requiring distributed locks or consensus.
- In failure scenarios (process crash after claim, before ack), a record may be processed more than once — this is the at-least-once trade-off.
