---
layout: default
title: "Persistent Events"
nav_order: 5
---

# Persistent Events

Persistent events add durability to EventBrokerSlim. Events and retry state survive process restarts and crashes. Multiple instances of the same application can process events concurrently, with an [at-least-once](#at-least-once-delivery) delivery guarantee.

Persistence is a configuration-only addition. Event types, publisher code, and handler code are unchanged in both modes.

## How persistent events work

With persistence enabled, publishing writes event records to a storage backend and immediately returns, instead of dispatching them in-process. A background polling loop fetches scheduled records from storage, claims them using optimistic concurrency, and dispatches each to the handler registered under its name. After execution, each record is marked complete, rescheduled for retry, or dead-lettered.

You need three things:
1. **`EventRegistry`**. Maps event types to stable string names.
2. **Handler names**. Links each handler registration to its storage records.
3. **A storage backend**. Configured via the builder.

## Event registry

`EventRegistry` maps each event type to a stable string name, stored alongside the payload as the record's identifier. It is not derived from the C# type name, so you can rename classes and namespaces freely without breaking existing records.

```csharp
var registry = new EventRegistry()
    .Add<ArticlePublished>("article-published")
    .Add<ArticleDeleted>("article-deleted");

services.AddSingleton(registry);
```

Every event type that participates in persistence, whether published or handled, must be registered here. Registration is a singleton. Define it once, register it once.

## Handler names

Only handlers registered with an explicit `handlerName` participate in persistent fan-out. When an event is published, one record is written per named handler. A handler registered without a name is silently inert: it is not flagged by startup validation and never runs, because dispatch flows entirely through storage and only named handlers are written. The same registration runs normally in-memory when persistence is not configured.

```csharp
services
    .AddEventHandlerPipeline<ArticlePublished>(emailPipeline, handlerName: "article-published-email")
    .AddTransientEventHandler<ArticleDeleted, DeletionHandler>(o => o.WithHandlerName("article-deleted-handler"));
```

Prefer the configuration delegate over positional arguments here. The registration methods take several optional string and key parameters in sequence, so passing the handler name positionally risks landing it in the wrong slot. `WithHandlerName()` names the intent. See [In-memory event broker: Registering handlers](04-in-memory-broker.md#registering-handlers).

Handler names are permanent. A record is stored under its handler name and later claimed by matching that name, so renaming a handler after records exist orphans them: records written under the old name match no handler and stay unclaimed until the unclaimed TTL dead-letters them.

## Configuring a backend

Each backend is a pluggable `IEventStorage` implementation, shipped as a separate NuGet package. Select one by chaining its extension method onto the broker builder inside `AddEventBroker()`. The method name, connection settings, and any one-time setup are backend-specific. See the [backend pages](#choosing-a-backend).

## Serialization

The core library carries no serializer. Serializing the event payload is the responsibility of each `IEventStorage` implementation, in whatever format suits its backend. This is what keeps the core library AOT-compatible.

Two things follow for your event types. Every type you persist must be serializable by its backend. Its payload structure is also a contract: the event name survives class and namespace renames (see [Event registry](#event-registry)), but renaming, removing, or retyping a property breaks deserialization of records already in the store.

## Starting the broker

Call `UsePersistentEventBroker()` to run startup validation and start the polling and maintenance loops:

```csharp
var provider = services.BuildServiceProvider();
provider.UsePersistentEventBroker(throwOnValidationErrors: true);
```

Startup validation checks that every handler with a `handlerName` has its event type in `EventRegistry`, and every event in `EventRegistry` has at least one named handler. By default, mismatches produce warnings. Pass `throwOnValidationErrors: true` to fail fast.

## Publishing

Publishing uses the same `Publish()` API as the in-memory broker. See [In-memory event broker: Publishing an event](04-in-memory-broker.md#publishing-an-event).

With persistence enabled, each call writes one record per named handler to the store before returning. The write happens inline, so if the store is unavailable the call fails and the exception surfaces to the caller. This is a change from the in-memory broker, where publishing enqueues in process and does no I/O. The publishing instance signals its polling loop immediately, so it does not wait for the next interval to dispatch. Other instances pick the records up on their next poll.

### Deferred publishing

`PublishDeferred()` writes one record per named handler with a future scheduled time computed from the specified delay. The polling loop picks them up when that time arrives. The records survive process restarts.

From its scheduled time onward, the record enters the normal lifecycle: claiming, dispatch, retry, and dead-lettering all apply.

## Event record lifecycle

An event record is the per-handler unit written to storage. Its `Status` is an `EventStatus`: it is created as `Scheduled`, then claimed into `InProgress` for dispatch. From there:

It moves to `Completed` when the handler returns without requesting a retry or abandoning.

It returns to `Scheduled` when the handler calls `retry.RetryAfter(...)`, after the specified delay.

It moves to `DeadLettered` when:
- The retry policy is exhausted and `Abandon()` is called.
- An exception escapes the pipeline without being caught.
- The processing-timeout counter reaches `MaxProcessingTimeouts`.
- The record remains unclaimed past `UnclaimedTtl`.

## Maintenance loops

Three independent background tasks run alongside the polling loop: processing-timeout reset, unclaimed-TTL dead-lettering, and retention cleanup. Each loop runs with random jitter to reduce contention when multiple instances are running.

- **Processing timeout reset.** Resets `InProgress` records whose claim time is older than `ProcessingTimeout` back to `Scheduled`, incrementing an internal counter. Once the counter reaches `MaxProcessingTimeouts`, the record is dead-lettered instead. Set `ProcessingTimeout` above the slowest expected handler execution time. Too short and records are incorrectly re-dispatched while the original handler may still be running. Too long and records from crashed instances remain stuck. Runs roughly every 5 minutes (default).
- **Unclaimed TTL.** Dead-letters `Scheduled` records that have been eligible for claiming (past their scheduled time) for longer than `UnclaimedTtl`. This handles events published for handler names that no consumer instance came online to process. The window is measured from the scheduled time, not from when the event was published, so a long deferred publish does not consume unclaimed time before the record is eligible. Set `UnclaimedTtl` above the maximum expected gap between a publish and a consumer starting. Runs roughly every hour (default).
- **Retention cleanup.** Deletes `Completed` and `DeadLettered` records past their respective TTL settings. Keeps the store from growing without limit. Runs roughly every hour (default).

## At-least-once delivery

In the normal case, a record is processed exactly once. But if a process crashes after claiming a record and before completing it, the record will be reset by the processing timeout reset loop and dispatched again. This means a handler can run more than once for the same record.

**Handlers must be idempotent.** This is a standard constraint for any reliable messaging system.

## Configuration reference

These settings are shared across all backends via `PersistentEventBrokerSettings`, the second parameter in the backend configuration delegate.

| Setting                 | Default    | Notes                                                                                                                                              |
| ----------------------- | ---------- | -------------------------------------------------------------------------------------------------------------------------------------------------- |
| `PollingInterval`       | 10 seconds | How often the loop checks for scheduled records. The publishing instance signals immediately on publish; other instances wait up to this interval. |
| `ProcessingTimeout`     | 5 minutes  | `InProgress` records older than this are reset to `Scheduled`. Must be longer than your slowest handler.                                           |
| `MaxProcessingTimeouts` | 10         | After a record has been reset by the processing-timeout loop this many times, it is dead-lettered.                                                 |
| `ScheduledBatchSize`    | 10         | Records fetched per poll cycle. Increase if throughput is limited by batch size.                                                                   |
| `UnclaimedTtl`          | 7 days     | `Scheduled` records not claimed within this period are dead-lettered.                                                                              |
| `CompletedRecordTtl`    | 7 days     | `Completed` records are deleted after this.                                                                                                        |
| `DeadLetteredRecordTtl` | 30 days    | `DeadLettered` records are retained for this long before deletion.                                                                                 |

Each maintenance loop's execution interval is independently configurable as a jittered, two-phase schedule. The defaults are the cadences noted under [Maintenance loops](#maintenance-loops); most deployments leave them unchanged.

## Publisher-only topology

A process that only publishes events, without processing them locally, can use `NullPipeline.Instance`. This registers the handler name for fan-out (so records are written on publish) without claiming or processing those records locally.

```csharp
services.AddEventHandlerPipeline<ArticlePublished>(NullPipeline.Instance, handlerName: "article-published-email");
```

Separate consumer instances with real handlers registered under the same name claim and process the records.

## Dead-letter handling

Dead-lettered records are retained for `DeadLetteredRecordTtl` (default 30 days) and then deleted. They are not retried automatically.

For production use, you need:
- Monitoring that alerts when records enter dead-letter state.
- A way to inspect and optionally requeue dead-lettered records (direct database access or custom tooling).

There is no built-in dead-letter UI.

## Design decisions

These are the non-obvious choices and their tradeoffs.

- **Fan-out at write time.** `Publish()` writes one independent record per handler name to the store. A fan-out-at-read-time alternative (one event record plus subscription offsets per handler) introduces a bootstrapping problem: events published before a handler subscribes are missed. Completion also becomes a heuristic. The tradeoff is payload duplication: each handler record carries the full serialized event.

- **Polling is the sole dispatch path.** Even a freshly published event goes through storage before dispatch. Fresh events and crash-recovered events follow the same code path, with no ambiguity about which path handled a record. The event broker wakes the polling loop after a write to minimize added latency.

- **Optimistic concurrency, no distributed locks.** The polling loop fetches all `Scheduled` records and discards those with no local handler. It then attempts a conditional claim immediately before processing. Each backend expresses the same conditional update in its own idiom (conditional SQL UPDATE, MongoDB `findOneAndUpdate`, Redis Lua script). Records from crashed instances are recovered by the processing timeout maintenance loop.

- **Escaped exceptions dead-letter immediately.** An exception that escapes the pipeline dead-letters the record without consulting the retry policy. Error handling is explicit: a handler that wants retry-on-exception must implement that decision in `OnError()` or an error-handling delegate.

## Choosing a backend

| Backend                                    | Best for                                             | Scale-out |
| ------------------------------------------ | ---------------------------------------------------- | --------- |
| [SQLite](06-persistence-sqlite.md)         | Dev, test, single-instance embedded                  | No        |
| [LiteDB](07-persistence-litedb.md)         | Dev, test, single-instance document store            | No        |
| [PostgreSQL](09-persistence-postgresql.md) | Production, relational, strong consistency           | Yes       |
| [SQL Server](10-persistence-sqlserver.md)  | Production, already on SQL Server                    | Yes       |
| [MongoDB](08-persistence-mongodb.md)       | Already running MongoDB, document-oriented workloads | Yes       |
| [Redis](11-persistence-redis.md)           | Already running Redis, fast polling                  | Yes       |

Embedded backends (SQLite, LiteDB) require no server. They are single-writer. Horizontal scale-out is not supported. Use them for development and single-instance production scenarios.

Server-based relational backends (PostgreSQL, SQL Server) support full horizontal scale-out and are the recommended choice for production multi-instance deployments.

MongoDB and Redis are good choices if your infrastructure already includes them and you want to avoid adding a relational database dependency. Redis is memory-first: records survive a restart only if Redis persistence is enabled.

The six backends implement a common `IEventStorage` contract. If none fits your store, you can [implement it for a custom backend](12-implementing-a-backend.md).
