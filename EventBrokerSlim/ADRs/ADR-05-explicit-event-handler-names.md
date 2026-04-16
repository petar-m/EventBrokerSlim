# ADR-05: Event and handler names decouple the store from C# type names

**Status:** Accepted  
**Date:** 2026-03-26

## Context

The store needs stable identifiers for event types and handlers that persist across deployments. C# type names are fragile as long-lived store identifiers — namespaces get reorganized, classes get renamed. Additionally, delegate/pipeline handlers have no distinguishable type name to derive an identifier from, since the handler is an `IPipeline` instance.

## Decision

Event types are registered in `EventRegistry` with explicit stable string names. Handler registrations supply an explicit `handlerName`. These names — not C# type names — are stored in the event store as the identifiers for event types and handlers respectively.

`EventRegistry` is defined once by the application and registered in DI as a singleton:

```csharp
var registry = new EventRegistry()
    .Add<OrderPlaced>("order-placed")
    .Add<OrderCancelled>("order-cancelled");

services.AddSingleton(registry);
```

Handler names are supplied at registration time via the optional `handlerName` parameter on both class-based and delegate handler registration methods. The parameter is optional — handlers without a `handlerName` participate only in in-memory dispatch and are invisible to the persistence layer. This preserves the non-breaking guarantee: existing handler registrations require no changes unless they need to participate in persistence.

## Consequences

- Decoupling store identifiers from type names means namespace and class renames are non-breaking refactors.
- The explicit name is the only viable approach for delegate/pipeline handlers, and using it uniformly across both handler kinds keeps the registration API consistent.
- Handler and event names must be stable across deployments — changing either is a breaking change requiring a migration. In-flight records under the old name will never be claimed.
- Property-level changes (renamed, removed, or retyped fields) remain breaking regardless, as deserialization depends on the payload structure.
