# ADR-10: Periodic maintenance for recovery and cleanup

**Status:** Accepted  
**Date:** 2026-03-26

## Context

Records claimed by instances that subsequently crash remain in `InProgress` state indefinitely. Records that are never claimed (due to removed handlers or missing consumers) remain `Scheduled` indefinitely. Completed and dead-lettered records accumulate in the store without bound.

Each of these failure modes requires a periodic process to detect and resolve. The claiming mechanism itself ([ADR-03](ADR-03-optimistic-claiming-broad-polling.md)) ensures at most one instance processes a record under normal operation, but does not handle recovery from crashes or cleanup of terminal records.

## Decision

A `MaintenanceRunner` in the core library runs three independent maintenance loops, each with random jitter on startup and between runs to prevent contention across instances:

- **`RescheduleClaimedExceedingProcessingTimeoutAsync`** — runs on a cadence tied to `ProcessingTimeout`; resets `InProgress` records where `claimed_at` exceeds `ProcessingTimeout` back to `Scheduled` with `scheduled_at = now`, incrementing `processing_timeouts_count`; dead-letters instead of reset once `MaxProcessingTimeouts` is exceeded
- **`DeadLetterUnclaimedAsync`** — runs hourly by default; dead-letters `Scheduled` records that have not been claimed within `UnclaimedTtl` measured from `scheduled_at`
- **`DeleteCompletedAndDeadLetteredExceedingTtlAsync`** — runs hourly by default; deletes `Completed` and `DeadLettered` records beyond their respective retention periods

The schedule is owned by the core library; each backend implements the three methods on `IEventStorage` using configuration supplied at backend registration time.

## Consequences

- The processing timeout must be tuned to be longer than the longest expected handler execution time; too short and `InProgress` records are incorrectly reset to `Scheduled` and dispatched again. Too long and records from crashed instances remain stuck until the next maintenance run.
- `MaxProcessingTimeouts` caps how many times a record can be reset before being dead-lettered, preventing indefinite cycling of a persistently stuck record.
- The unclaimed timeout must be set comfortably above the expected maximum time between a publish and a consumer coming online.
- Random jitter on startup and between runs prevents multiple instances from running maintenance at the same time, reducing contention on the store.
- Completed and dead-lettered record retention is bounded, keeping the store from growing without limit.
