# ADR-01: Fan-out at write time

**Status:** Accepted  
**Date:** 2026-03-26

## Context

The in-memory broker already fans out every event to all registered handlers. The persistence layer needs to replicate this behavior durably. Two strategies were considered:

- **Fan-out at write time** - write one record per handler at publish time.
- **Fan-out at read time** - write one event record and have each handler track its own offset or subscription.

Fan-out at read time requires a durable subscription registry, introduces a bootstrapping problem (events published before a handler registers its subscription are missed), and makes completion tracking a heuristic rather than a deterministic check.

## Decision

When `Publish` is called, the persistence layer resolves all handler names registered for that event type from the local DI container and writes one independent record per handler name to the store. The event is identified in the store by its name from `EventRegistry`, not its C# type name.

## Consequences

- The store contains the full set of work the publishing instance knows about.
- Any instance with a matching handler registered will claim and process its records independently. Instances without a matching handler ignore those records.
- Records that no instance ever claims are eventually cleaned up by TTL and retention policies.
- A publisher-only process can use `NullPipeline` to register handler names for fan-out without local processing. Records are written to the store for those handler names, but the publishing instance does not claim or dispatch them. Separate consumer instances with real handlers claim and process the records.
- Queue backends become impractical - they would require one queue per handler name, essentially using a queue as a queryable database with worse querying capabilities.
