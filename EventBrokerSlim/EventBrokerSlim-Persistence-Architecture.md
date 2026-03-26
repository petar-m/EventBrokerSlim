# EventBrokerSlim Persistent Events
## Goals, Assumptions, and Architecture Decisions

---

## Purpose

This document describes the goals, assumptions, and architecture decisions behind the persistent events feature of `EventBrokerSlim`. It is intended for contributors seeking to understand the reasoning behind the design, and for adopters evaluating whether the feature fits their use case.

It does not cover implementation details or backend-specific concerns.

---

## When to Use This Feature

*Informally: a poor man's queue with fan-out.*

### The Landscape

Several .NET libraries address reliability and background processing. Understanding where they sit helps clarify where EventBrokerSlim fits.

**Job schedulers — Hangfire, Quartz.NET, Coravel, TickerQ.** These persist and execute explicitly enqueued work. You schedule a job, a worker picks it up and runs it. They support retries, recurring schedules, and monitoring dashboards. None of them have fan-out — if three things need to happen when an order is placed, you enqueue three jobs, and the coupling lives in the caller.

**Distributed messaging frameworks — MassTransit, Rebus, Wolverine, Brighter.** These support fan-out via message routing and provide durable delivery, sagas, and consumer registration. They abstract over real message brokers (RabbitMQ, Azure Service Bus) or provide their own transport. Capable and well-supported, but they are framework-level commitments with significant infrastructure and learning curve.

**Message brokers — RabbitMQ, Azure Service Bus, Amazon SQS.** The right tool for cross-service event distribution, heterogeneous consumers, and high-throughput messaging. Require dedicated infrastructure, operational expertise, and introduce a network hop and a new failure domain.

| Library | Fan-out | Persistence | Scheduling | Scope |
|---|---|---|---|---|
| EventBrokerSlim | ✓ | ✓ | ✗ | Narrow, focused |
| Hangfire | ✗ | ✓ | ✓ | Background jobs |
| Quartz.NET | ✗ | ✓ | ✓ | Job scheduling |
| Coravel | ✗ | ✗ | ✓ | Lightweight scheduling |
| TickerQ | ✗ | ✓ | ✓ | Time-based jobs |
| Wolverine | ✓ | ✓ | ✓ | Full framework |
| Brighter | ✓ | ✓ | ✗ | Command/event dispatch |
| MassTransit | ✓ | ✓ | ✓ | Distributed messaging |
| Rebus | ✓ | ✓ | ✗ | Distributed messaging |

### Where EventBrokerSlim Falls

EventBrokerSlim with persistent events sits in the gap between job schedulers and distributed messaging frameworks. It is not a job scheduler — there is no explicit enqueue, no cron scheduling, no job chaining. It is not a distributed messaging framework — there is no broker, no transport, no cross-service routing.

It is a **durable in-process event bus** — publish an event, every registered handler runs exactly once, reliably, even across crashes and restarts, across multiple instances of the same application.

### Advantages

**Fan-out with decoupling.** One publish, multiple independent handlers, each with its own delivery guarantee and retry policy. The publisher knows nothing about the handlers. Adding a new handler requires no change to the publisher.

**DI-native programming model.** Handlers are plain classes resolved from your DI container. Retry policies are code. No serialization contracts, no consumer group configuration, no broker-specific concepts to learn.

**Uses infrastructure you already have.** Persistence runs on your existing database — relational, document, or embedded. No new infrastructure to provision, operate, or monitor. For embedded backends (SQLite, Firebird) not even a database server is needed.

**Narrow scope, low commitment.** It does one thing — durable in-process fan-out — and does not grow into a framework. Adopting it does not constrain your architecture or require you to buy into a broader ecosystem.

**Can coexist with job schedulers.** EventBrokerSlim handles event-driven dispatch; Hangfire or Quartz.NET handle scheduled and background jobs. They solve different problems and work naturally alongside each other.

### When to Choose Something Else

- Cross-service event distribution, heterogeneous consumers → message broker or distributed messaging framework
- Scheduled or recurring background jobs → Hangfire, Quartz.NET, or TickerQ
- Event sourcing, audit log, replay → dedicated event store
- Complex workflow orchestration, sagas → MassTransit, Wolverine, or NServiceBus

### The Bottom Line

EventBrokerSlim with persistent events fills the gap below message brokers — where their operational overhead is not justified, but the in-memory-only broker is not reliable enough.

---

## Goals

**Durability.** Events published via `IEventBroker.PublishAsync` must not be lost if the process crashes before all handlers have completed. Every handler must eventually process every event it is registered for, even across process restarts.

**Durable retries.** Handler retry policies must survive process restarts. An event that failed and is awaiting a retry must remain in that state after a crash and be retried correctly when the process comes back up.

**Horizontal scale-out.** Multiple instances of the same application must be able to run concurrently without any handler processing the same event more than once.

**Non-breaking.** The existing public interface of `EventBrokerSlim` must remain unchanged. Existing handler code, retry policies, delegate handlers, and DI registrations must require zero modifications to benefit from persistence.

**Opt-in.** Persistence is an additive feature. Applications that do not need it are unaffected. It is enabled entirely through DI registration.

**Pluggable backends.** The persistence mechanism must be replaceable. Server-based relational databases (SQL Server, PostgreSQL), non-relational databases (MongoDB, Redis, CosmosDB), and embedded databases (SQLite, LiteDb, Firebird) must all be supportable through a common abstraction. Embedded backends target single-instance durability and development scenarios; server-based backends target production horizontal scale-out.

---

## Non-Goals

**Not an event sourcing solution.** The store is a delivery mechanism, not an event log. Records exist to ensure handlers execute reliably — they are not a source of truth, do not represent the full history of what happened in the system, and are not intended for replay, projection, or audit. Completed records are deleted according to retention policy. Applications that need event sourcing should use a dedicated solution.

**Not a transactional outbox.** The event write is not atomic with the caller's own database transaction. Supporting this would require sharing the caller's database connection and transaction, constraining the design to same-database relational deployments only and coupling the event store interface to transaction management concerns. Applications that need true transactional outbox semantics should use a dedicated solution such as MassTransit, NServiceBus, or Wolverine. `EventBrokerSlim` provides at-least-once delivery across process restarts, which is sufficient for the majority of durability use cases.

---

## Assumptions

**A record is only processed if a matching handler is registered.** When `PublishAsync` is called, records are written for the handler names known to the publishing instance. Any instance with a matching handler registered will eventually claim and process the record. An instance without that handler simply never claims it — it does not interfere and does not cause an error. If no running instance has the handler registered, the record remains pending until a TTL or retention policy cleans it up.

**At-least-once delivery is acceptable.** In failure scenarios (process crash after claim, before ack), an event may be dispatched to a handler more than once. Handlers should be idempotent. This is a standard and documented constraint, not a deficiency.

**Events are serializable.** Because events must be written to a store and later deserialized for replay, all event types must be serializable. Serialization format and mechanism are the responsibility of each `IEventStorage` implementation — the core library provides no shared serializer. This is a new requirement that does not exist in the in-memory-only version of the library.

**Handler names are explicit and stable.** Each handler participating in persistence must be registered with an explicit `handlerName`. This name is used as the handler identifier in the store. It is not derived from the type name — this is essential for delegate/pipeline handlers, where there is no distinguishable type to derive a name from. The name must be stable across deployments; changing it is a breaking change requiring a migration.

**Event names are explicit and stable.** Each event type participating in persistence must be registered in `EventNameRegistry` with a stable string name. The store uses this name rather than the C# type name, so namespace and class renames are non-breaking. Property-level changes (renamed, removed, or retyped properties) remain breaking because deserialization depends on the payload structure.

**Event names are globally unique across broker instances.** `EventNameRegistry` is a global singleton shared across all keyed broker instances in the same process. The same event type maps to exactly one name, and the same name maps to exactly one event type, regardless of which broker instance publishes or handles it. Event identity is unambiguous across the entire process — reasoning about events, their handlers, and store records is consistent whether working with a single broker or multiple. Applications with multiple broker instances share the same registry.

**The event store is external infrastructure.** The caller is responsible for provisioning and operating the backing database. The library provides schema scripts and client configuration, but operational concerns (backups, monitoring, scaling) are outside its scope.

---

## Architecture Decisions

### AD-1: Fan-out at write time

When `PublishAsync` is called, the persistence layer resolves all handler names registered for that event type from the local DI container and writes one independent record per handler name to the store. The event is identified in the store by its name from `EventNameRegistry`, not its C# type name.

**Why:** This mirrors exactly what the in-memory broker already does — every registered handler receives every event. Writing one record per handler at publish time means the store contains the full set of work the publishing instance knows about. Any instance with a matching handler registered will claim and process its records independently. Instances without a matching handler ignore those records. Records that no instance ever claims are eventually cleaned up by TTL and retention policies.

The alternative — fan-out at read time, where one event record is written and each handler tracks its own offset — was considered and rejected. It requires a durable subscription registry, introduces a bootstrapping problem (events published before a handler registers its subscription are missed), and makes completion tracking a heuristic rather than a deterministic check.

---

### AD-2: The polling service is the single dispatch path

When persistence is enabled, `IEventBroker` is implemented by a persistent broker that writes to the store and returns — no in-memory dispatch occurs at publish time. A background polling loop (`EventStoragePolling`) is the sole dispatch path. It continuously fetches candidate records, claims them using optimistic concurrency, and dispatches them through the in-memory handler machinery. Ack/nack/dead-letter results are written back to the store.

To reduce latency, the persistent broker signals the polling loop after a write — waking it up immediately rather than waiting for the next poll interval. This is an optimisation; the poller's correctness does not depend on it.

**Known limitation:** The polling loop and handler runner use `Task.Factory.StartNew` with `LongRunning` rather than `IHostedService`. Integration with the ASP.NET Core hosted service lifecycle is not currently implemented.

**Why:** A single dispatch path eliminates dual-path confusion — there is no ambiguity about whether an event was handled in-memory or via the store. It also means fresh events and replayed events after a crash follow exactly the same code path, making the system easier to reason about and test.

---

### AD-3: Claiming is atomic and timestamp-based; a periodic process handles recovery

The polling service fetches a batch of candidate records using a broad query — `status = 'Scheduled' AND scheduled_at <= now` — with no handler name filter. Candidates with no matching local handler are discarded in memory. The polling service then attempts to claim candidates one by one using optimistic concurrency: a conditional update that sets `status = InProgress` and `claimed_at = now` only if `status` is still `Scheduled` at update time. If another instance claimed the record first, the condition fails and the next candidate is tried. The mechanism varies by backend — a conditional `UPDATE` with row count check for SQL, `findOneAndUpdate` for MongoDB, an atomic Lua script for Redis, a conditional patch with ETag for CosmosDB — but the outcome is identical across all backends: at most one instance claims any given record.

No instance identifier is stored — with horizontal scaling there is no reliable stable identifier per instance, and none is needed.

A `MaintenanceRunner` in the core library runs three independent maintenance loops, each with random jitter on startup and between runs to prevent contention across instances:

- `RescheduleClaimedExceedingProcessingTimeoutAsync` — runs on a cadence tied to `ProcessingTimeout`; resets `InProgress` records where `claimed_at` exceeds `ProcessingTimeout` back to `Scheduled` with `scheduled_at = now`, incrementing `processing_timeouts_count`; dead-letters instead of reset once `MaxProcessingTimeouts` is exceeded
- `DeadLetterUnclaimedAsync` — runs hourly by default; dead-letters `Scheduled` records that have not been claimed within `UnclaimedTtl` measured from `scheduled_at`
- `DeleteCompletedAndDeadLetteredExceedingTtlAsync` — runs hourly by default; deletes `Completed` and `DeadLettered` records beyond their respective retention periods

The schedule is owned by the core library; each backend implements the three methods on `IEventStorage` using configuration supplied at backend registration time.

**Why:** A broad query is simpler and more efficiently indexable than a query filtered by a potentially large list of handler names. In-memory filtering before claiming avoids wasteful claim-then-release cycles in partial deployments. Optimistic concurrency keeps the claim mechanism uniform across backends — each backend expresses the same conditional update in its own idiom without requiring distributed locks or consensus.

---

### AD-4: One store table, one record per (event, handler name)

All records — regardless of event type or handler — live in a single table. The record schema includes the event name, handler name, serialized payload, status, attempt count, retry timestamp, claim metadata, and error information. The event payload is duplicated across all handler records for the same event.

**Why:** A single table keeps the schema simple and backend-agnostic. Querying by handler name and status is sufficient for all polling operations. Each record is fully self-contained — claiming, dispatching, retrying, and cleaning up a record requires no joins and no coordination with other records. The payload duplication is the accepted tradeoff: the alternative of separating events and handler records into two tables (one event row, multiple handler rows) eliminates duplication but adds a join on every poll query and complicates cleanup, since the event row can only be deleted once all its handler rows are in a terminal state.

---

### AD-5: Event and handler names decouple the store from C# type names

Event types are registered in `EventNameRegistry` with explicit stable string names. Handler registrations supply an explicit `handlerName`. These names — not C# type names — are stored in the event store as the identifiers for event types and handlers respectively.

`EventNameRegistry` is defined once by the application and registered in DI as a singleton:

```csharp
var registry = new EventNameRegistry()
    .Add<OrderPlaced>("order-placed")
    .Add<OrderCancelled>("order-cancelled");

services.AddSingleton(registry);
```

Handler names are supplied at registration time via the optional `handlerName` parameter on both class-based and delegate handler registration methods. The parameter is optional — handlers without a `handlerName` participate only in in-memory dispatch and are invisible to the persistence layer. This preserves the non-breaking guarantee: existing handler registrations require no changes unless they need to participate in persistence.

**Why:** C# type names are fragile as long-lived store identifiers — namespaces get reorganized, classes get renamed. Decoupling store identifiers from type names means these are non-breaking refactors. The explicit name is also the only viable approach for delegate/pipeline handlers, where the handler is an `IPipeline` instance and no distinguishable type name exists. Using explicit names uniformly across both handler kinds keeps the registration API consistent.

---

### AD-6: Startup validation

When persistence is enabled, the application performs eager validation on startup before accepting any traffic:

- Every handler registered with a `handlerName` must have its event type present in `EventNameRegistry`
- Every event type in `EventNameRegistry` should have at least one handler with a `handlerName` registered — a warning is emitted if not, as events of that type will be written to the store and never claimed
- Attempting to publish an event persistently whose type is not in `EventNameRegistry` throws at publish time
- Attempting to publish an event persistently when any of its registered handlers is missing a `handlerName` throws at publish time

Attempting to publish an event persistently without a valid event name or with a handler missing a name will throw from `PublishAsync` as a last-resort defensive check. This should never be reached in a correctly configured application — startup validation is the primary safeguard. The throw exists to prevent silent data loss in cases where startup validation was bypassed or misconfigured.

**Why:** Surfacing misconfiguration at startup rather than at first publish means problems are discovered immediately and deterministically, not buried in a rarely-exercised code path. The runtime throw is a defensive backstop, not the expected error handling mechanism.

---

### AD-7: Escaped pipeline exceptions always dead-letter; the retry policy is not consulted

If an exception escapes the pipeline without being handled, the event record is immediately dead-lettered. The retry policy is not consulted in this case — `RetryRequested` and `Abandoned` are ignored.

**Why:** The pipeline contract guarantees that exceptions are handled within the pipeline — via an error handling delegate or `IEventHandler<TEvent>.OnError`. An escaped exception means the handler failed to honour that contract. Consulting the retry policy after an unhandled exception would allow handlers to abuse the mechanism — throwing deliberately while setting `RetryRequested` to get retry behaviour without explicit error handling. Dead-lettering on escaped exceptions enforces the contract and keeps error handling explicit: a handler that wants retry-on-exception must implement that decision in `OnError` or an error handling delegate.

---

### AD-8: Serialization is the responsibility of IEventStorage implementations

The core library does not provide a shared serializer. Each `IEventStorage` implementation is responsible for serializing and deserializing event payloads in whatever format suits the backend. `EventRegistry` is passed to `FetchScheduledAsync` so the backend can resolve CLR types from event names during deserialization.

**Why:** `EventBrokerSlim` is AOT-compatible. A shared JSON serializer based on reflection would break AOT-compatible deployments. Beyond AOT, serialization format is legitimately a backend concern — a Redis backend may prefer MessagePack, a PostgreSQL backend may prefer JSON, a custom backend may use Protobuf or stream large payloads differently. Offloading serialization to the backend keeps the core library free of serialization dependencies and allows each backend to make the choice that best fits its constraints, including AOT compatibility.

---

## Out of Scope

### Queue backends (RabbitMQ, Azure Storage Queues, Azure Service Bus)

Queue backends were explored and excluded. The fan-out at write time strategy requires one queue per handler name — each handler type needs its own queue so that messages can be consumed independently. This is viable but means using a queue as a queryable database with worse querying capabilities. A database does this job better and more simply.

More fundamentally, the moment you need a queue with fan-out and independent per-handler delivery, you are in message broker territory. That problem is already solved by RabbitMQ exchanges, Azure Service Bus topic subscriptions, and similar infrastructure. Replicating that inside `EventBrokerSlim` would be building a message broker, which is explicitly not the goal.

### Publisher-only processes

A process with no handler registrations writes zero records to the store — it has nothing to fan out to. Events published from such a process are silently lost. This is a direct consequence of fan-out at write time: the publisher writes records only for handlers it knows about in its own DI container.

Partial deployments — where different instances have different subsets of handlers — work naturally. Each instance writes records for the handlers it knows about, and any instance with a matching handler will eventually claim and process them. Records for handlers that no instance ever registers remain pending until TTL or a retention policy cleans them up.

The publisher-only topology — a dedicated process with no handlers whose sole role is publishing — falls into distributed event broker territory and is out of scope. Supporting it would require fan-out at read time, which introduces a subscription registry, a bootstrapping problem, and heuristic completion tracking. `EventBrokerSlim`'s identity is in-process fan-out, and every publishing process is expected to carry at least some handler registrations.

### Cross-service event distribution

Distributing events across heterogeneous services — where different services handle different event types, or where a publisher and consumer are entirely separate applications — is out of scope. This is the core problem that full-scale message brokers exist to solve, and `EventBrokerSlim` deliberately does not compete in that space.

The persistent events feature targets a single application scaled horizontally. The store is a durability mechanism, not a transport.

---

## Known Limitations

**`IEventBroker.PublishDeferred` does not accept a `CancellationToken`.** The method signature does not expose a cancellation token parameter. Deferred publishes use the broker's internal shutdown token and cannot be cancelled by the caller. This is a known interface limitation inherited from the base `IEventBroker` contract and is not currently planned for change.

**No `IHostedService` integration.** The polling loop and maintenance runner use long-running background tasks rather than the ASP.NET Core `IHostedService` lifecycle. This means they are not subject to graceful shutdown coordination via `IHostedService.StopAsync`. Shutdown is handled via the shared `CancellationTokenSource` cancelled by `IEventBroker.Shutdown()`.

---

## Operational Considerations for Adopters

Adding persistence introduces real operational overhead that does not exist with the in-memory-only broker. Adopters should be aware of:

**Database dependency.** The application now depends on an external store being available. Store unavailability will cause `PublishAsync` to fail.

**Schema management.** Each backend package provides migration scripts. Schema changes across library versions must be applied as part of deployment.

**Dead-letter monitoring.** Records land in a dead-letter state when the retry policy is exhausted, when a handler explicitly calls `Abandon()`, or when an exception escapes the pipeline unhandled. Dead-lettered records are not retried automatically. Monitoring and tooling for dead-letter inspection and requeue is necessary for production use.

**Polling interval tuning.** The in-memory signal ensures the publishing process dispatches freshly published events with minimal latency. Other instances discover available work only on their next poll interval — during event spikes, idle instances join processing with a delay up to the polling interval. Shorter intervals reduce cross-instance latency at the cost of more store queries when idle.

**Processing timeout tuning.** The processing timeout must be longer than the longest expected handler execution time. Too short and `InProgress` records are incorrectly reset to `Scheduled` and dispatched again. Too long and records from crashed instances remain stuck until the next maintenance run. `MaxProcessingTimeouts` caps how many times a record can be reset before being dead-lettered, preventing indefinite cycling of a persistently stuck record.

**Unclaimed timeout tuning.** The unclaimed timeout determines how long a `Scheduled` record that is never claimed waits before being dead-lettered. This covers removed handlers, missing consumers, and deferred publishes that no instance ever processes. Should be set comfortably above the expected maximum time between a publish and a consumer coming online.

**Handler name stability.** The `handlerName` supplied at registration is stored in the event store as the handler identifier. Changing it is a breaking change — in-flight records under the old name will never be claimed. Treat handler name changes as migrations.

**Event name stability.** The name registered in `EventNameRegistry` is stored as the event type identifier. Changing it is a breaking change — existing records under the old name cannot be deserialized or claimed correctly. C# type renames and namespace changes are safe as long as the registered name does not change. Property-level changes (renamed, removed, or retyped fields) are breaking regardless, as deserialization depends on the payload structure.
