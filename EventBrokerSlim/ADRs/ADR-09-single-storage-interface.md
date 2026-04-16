# ADR-09: `IEventStorage` remains a single interface

**Status:** Accepted  
**Date:** 2026-03-28

## Context

A proposal was evaluated to split `IEventStorage` into four role-specific interfaces — `IEventScheduler`, `IEventPoller`, `IEventProcessor`, `IEventMaintenance` — to address an Interface Segregation Principle (ISP) violation. Each internal consumer (`PersistentEventBroker`, `EventStoragePolling`, `EventHandlerRunner`, `MaintenanceRunner`) uses a distinct subset of the interface's 10 methods.

Two alternatives to the current design were considered:

1. **Four separate interfaces** — misleadingly suggests the interfaces can be implemented independently, introduces a discoverability problem requiring implementors to find and implement four mandatory interfaces.
2. **Composite interface** (`IEventStorage : IEventScheduler, ...`) — reintroduces the same coupling the segregation aims to eliminate, since consumers could depend on the composite type. Removing the composite and relying on runtime DI validation to enforce completeness trades compile-time safety for runtime checks.

## Decision

The proposal was rejected. `IEventStorage` remains a single interface.

## Consequences

- The ISP violation is entirely internal — all consumers of `IEventStorage` are non-public classes within the core library. External users interact with `IEventBroker`, never with `IEventStorage` directly. The violation is contained and does not leak to adopters.
- For implementors — the actual audience of the public `IEventStorage` contract — a single interface signals that a storage provider must implement all operations. There is no valid partial implementation.
- A single interface provides compile-time completeness via "Implement Interface" in the IDE.
