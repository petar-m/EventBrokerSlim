---
title: EventBrokerSlim
---

# EventBrokerSlim

EventBrokerSlim is an in-process, fire-and-forget event bus for .NET. You publish an event and every registered handler runs independently. Add persistence and handler execution survives process crashes, restarts, and horizontal scale-out. All of this is backed by your existing database and requires no changes to publishing or handling code.

It sits between in-process notification libraries like MediatR and messaging libraries like MassTransit. Broader than MediatR: fire-and-forget dispatch, optional persistence, cross-process fan-out, built-in retries. Narrower than MassTransit: no broker, no transport, no cross-service routing.

## What it does

- **Fire-and-forget dispatch.** Publishing an event is non-blocking. Every registered handler runs on the thread pool, in its own DI scope. Concurrency is configurable.

- **Fan-out to independent handlers.** Publishing an event triggers every registered handler. Adding or removing a handler requires no change to the publisher. Features stay decoupled; the app stays coherent.

- **Per-handler retry policies.** Each handler decides in code when to retry, how long to wait, and how many attempts to allow. A handler that retries does not affect the others processing the same event.

- **Pipeline composition.** Pipelines let you layer cross-cutting concerns (logging, telemetry, validation, error handling) around business logic. Each step is independently testable. Class-based and delegate-based pipeline handlers coexist on the same event type.

- **Persistence on a database you already run.** When you need events to survive process restarts, enable persistence backed by a database you already have. Additional packages cover embedded (SQLite, LiteDB), relational (PostgreSQL, SQL Server), document (MongoDB), and key-value (Redis) stores. Start in-memory and switch to persistent with configuration only.

- **AOT-compatible.** For NativeAOT deployments (trimmed containers, mobile, WASM), the in-memory core has no reflection-based serialization or runtime code generation. The persistence packages are not AOT-compatible by default, but the pluggable storage architecture makes AOT-compatible storage backends possible.

- **Cross-instance fan-out without coordination.** When multiple instances share a persistent store, each claims and processes records independently via optimistic concurrency. No distributed locks, no leader election, no coordination service.

- **Isolated brokers per process.** Each broker instance in the process has its own handlers, concurrency settings, and dispatch channel. Subsystems that need separate event spaces coexist in the same DI container without sharing handlers or configuration.

## A typical evolution path

> "A complex system that works is invariably found to have evolved from a simple system that worked. The inverse proposition also appears to be true: A complex system designed from scratch never works and cannot be made to work. You have to start over, beginning with a working simple system."
>
> John Gall

This library exists to support a specific way of growing a system. Start small. Add complexity only when the system asks for it.

A common path looks like this:

1. **Single process, modular.** You organize the app as modules in one process. The in-memory broker decouples them. Modules publish events and don't know who handles them. Zero infrastructure beyond the app itself.

2. **Add persistence.** When events need to survive process restarts, enable persistence backed by a database you already have. The change is configuration; publisher and handler code stay identical.

3. **Split processes.** Some modules move into their own processes for independent scaling or deployment. They share the same store. Cross-instance fan-out happens through polling. Code, again, stays identical.

4. **Outgrow it.** When you need cross-service routing, exactly-once semantics, or dedicated broker tooling, adopt a message broker as new infrastructure. Using a library like MassTransit on top of the broker is optional. Your handler logic carries over; what changes is the publish call and how handlers are registered, not the business logic inside them.

Each step is small. You only pay for the next stage's complexity when you actually need it. Step 4 is an expected graduation, not a failure. The library is shaped to make every step short.

## Design tradeoffs

**At-least-once, not exactly-once.** With persistence enabled, a crash between claiming a record and acknowledging it can cause a handler to run more than once. Handlers must be idempotent.

**Persistence needs a database.** You're not adding infrastructure in the sense of a new server. But you are adding a database dependency if you don't have one. If the database is unavailable, publishing will fail.

**Multi-handler events multiply payload storage.** With persistence enabled, one record per handler is written on each publish, each carrying the full serialized payload. Three handlers on the same event means the payload is stored three times. Factor this into storage sizing for large payloads or high-frequency events.

**Cross-instance dispatch has polling latency.** When multiple instances share a store, instances other than the publisher discover new work on the next poll cycle (default: 10 seconds). The publishing instance dispatches immediately via an in-memory signal. For low-latency cross-instance fan-out, tune the polling interval.

**No built-in dead-letter tooling.** A record dead-letters when the retry policy gives up, an exception escapes the pipeline, processing repeatedly times out, or it remains unclaimed past its TTL. Dead-lettered records are retained for a configurable period and then deleted. There is no built-in UI or requeue mechanism. You need your own tooling or direct database access.

**No response channel.** Publishing is fire-and-forget. The publisher returns before any handler runs; there is no mechanism to receive a result from a handler.

**Not event sourcing.** EventBrokerSlim marks records as completed after delivery and deletes them after a configurable retention window. The store is a delivery mechanism, not a record of past events.

**Not a transactional outbox.** Writes to the event store are not atomic with your own database transaction. Publish-or-rollback semantics with your domain write are not supported.

## How it compares

| Library             | Fan-out dispatch | Request-response | Persistence | Scheduling | Broker required | Scope                                          |
| ------------------- | ---------------- | ---------------- | ----------- | ---------- | --------------- | ---------------------------------------------- |
| **EventBrokerSlim** | ✓                | ✗                | ✓           | ✗          | ✗               | In-process event bus with optional persistence |
| MediatR             | ✓                | ✓                | ✗           | ✗          | ✗               | In-process requests/notifications              |
| MessagePipe         | ✓                | ✓                | ✗           | ✗          | ✗               | High-performance in-process pub/sub            |
| Hangfire            | ✗                | ✗                | ✓           | ✓          | ✗               | Background jobs                                |
| Quartz.NET          | ✗                | ✗                | ✓           | ✓          | ✗               | Cron-style job scheduling                      |
| Brighter            | ✓                | ✓                | ✓           | ✗          | optional        | Command/event dispatch                         |
| CAP                 | ✓                | ✗                | ✓           | ✗          | ✓               | Outbox-pattern event bus                       |
| MassTransit         | ✓                | ✓                | ✓           | ✓          | ✓               | Distributed messaging                          |
| Wolverine           | ✓                | ✓                | ✓           | ✓          | optional        | Messaging + web framework                      |

EventBrokerSlim fills the gap between in-process notification libraries and distributed messaging libraries. The first offer no path to durability. The second require a broker and the operational footprint that comes with it. EventBrokerSlim is designed to hand off cleanly to a messaging library when you outgrow it.

The closest in-process alternative is MediatR. The key difference is dispatch. MediatR's publish awaits all handlers before returning, sequentially by default or in parallel depending on configuration. EventBrokerSlim is fire-and-forget. MediatR has no persistence, retry, or cross-process model. For request/response, MediatR remains the right choice. EventBrokerSlim has no return value.

MessagePipe is a high-performance typed pub/sub with pipeline middleware. Its synchronous variant runs handlers inline. Its async publisher awaits all handlers before returning, sequentially or in parallel depending on configuration. Fire-and-forget semantics require the caller to discard the returned task. There is no built-in durable storage. Companion packages extend reach to Redis and inter-process channels but do not add durability.

Job schedulers (Hangfire, Quartz.NET) persist and execute explicitly enqueued work but don't fan out. Enqueuing returns immediately; the job runs on a background worker later. Multiple handlers on the same trigger require manual enqueuing.

Brighter is a command processor with outbox, retry, and circuit-breaker support. Its in-process dispatch is blocking: the caller awaits handler completion before returning. A separate broker path writes to a local outbox and forwards messages to an external broker, where consumers process them out-of-process.

CAP is the .NET implementation of the outbox pattern: events are written to a local outbox table inside the application's database transaction and delivered asynchronously to a broker. The publish call returns after the write, before any handler runs. This gives true publish-or-rollback semantics with your domain write, which EventBrokerSlim explicitly does not provide. CAP requires a message broker; EventBrokerSlim does not.

Cross-service routing requires a message broker (RabbitMQ, Azure Service Bus, Kafka, AWS SQS) as new infrastructure. Messaging libraries (MassTransit, Wolverine) sit on top of brokers and add outbox, sagas, and transport abstractions. EventBrokerSlim is neither a broker nor a library on top of one. It has no broker, no transport, no cross-service routing.

## Next steps

- [Getting Started](02-getting-started.md). A working example in minutes.
- [Pipelines](03-pipelines.md). The recommended handler model: compose handlers from small, testable functions.
- [In-memory broker](04-in-memory-broker.md). Deep dive into how dispatch works, the handler lifecycle, and retry policies.
- [Persistent Events](05-persistent-events.md). Everything you need to add durability.
