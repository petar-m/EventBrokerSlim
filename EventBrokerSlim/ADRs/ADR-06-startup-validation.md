# ADR-06: Startup validation

**Status:** Accepted  
**Date:** 2026-03-26

## Context

Misconfiguration - such as a handler registered with a `handlerName` ([ADR-05](ADR-05-explicit-event-handler-names.md)) whose event type is missing from `EventRegistry`, or an event type in `EventRegistry` with no corresponding handler - can cause silent data loss or orphaned records. Discovering these problems only at first publish means they are buried in rarely-exercised code paths.

## Decision

When persistence is enabled, the application performs eager validation on startup before accepting any traffic:

- Every handler registered with a `handlerName` must have its event type present in `EventRegistry`
- Every event type in `EventRegistry` should have at least one handler with a `handlerName` registered - otherwise, events of that type are silently skipped at publish time because there are no handler names to fan out to and no records are written to the store. `NullPipeline` registrations satisfy this rule - they provide handler names so that records are written to the store, but the registering instance does not claim or process them. At least one other instance must have a real handler registered under the same name to claim and process the records.

By default, both rules emit warnings via the configured logger. Applications that want stricter enforcement can opt in to throwing on validation errors by passing `throwOnValidationErrors: true` to `UsePersistentEventBroker`.

Publishing an event whose type is not in `EventRegistry` is handled the same way as the in-memory broker handles events with no registered handlers: a warning is logged and the event is silently skipped. This matches the in-memory broker's behavior, preserving the seamless switch between in-memory and persistent modes. The warning respects the `DisableMissingHandlerWarningLog` setting.

## Consequences

- Problems are discovered immediately and deterministically at startup rather than at first publish.
- Defaulting to warnings keeps the library non-breaking for development and gradual migration scenarios.
- The opt-in throw gives production deployments a strict fail-fast guarantee.
- The publish-time behavior mirrors the in-memory broker to maintain consistent fire-and-forget semantics regardless of the persistence mode - code that works with the in-memory broker must not break when switching to persistence.
