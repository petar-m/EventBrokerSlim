---
layout: default
title: "Implementing a storage backend"
nav_order: 12
---

# Implementing a storage backend

EventBrokerSlim ships six storage backends: SQLite, LiteDB, MongoDB, PostgreSQL, SQL Server, and Redis. When your store is none of these, you can add it by implementing one interface: `IEventStorage`.

This page assumes you have read [Persistent Events](05-persistent-events.md) and know the event record lifecycle it describes.

`IEventStorage` is a single interface, and every backend implements all of it. There is no partial implementation. One method, `TryClaimAsync()`, carries a correctness requirement the whole design rests on: a store that cannot claim a record atomically cannot deliver events safely. That method has its own section below.

## The `IEventStorage` contract

The interface has ten methods. The event broker calls them across the four phases of a record's life; you implement what each does to the store.

| Phase       | Method                                             | Must do                                                                                                                                                        |
| ----------- | -------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Schedule    | `ScheduleAsync`                                    | Write one record per handler name. Status `Scheduled`, due now. Serialize the payload here.                                                                    |
| Schedule    | `ScheduleDeferredAsync`                            | Same, due after the given delay.                                                                                                                               |
| Poll        | `FetchScheduledAsync`                              | Return up to `batchSize` due `Scheduled` records, oldest first, as lightweight `ScheduledEventRecord` (no payload).                                            |
| Poll        | `TryClaimAsync`                                    | Atomically flip one candidate to `InProgress` only if still unchanged since fetch; deserialize and return the `EventRecord`, else `EventRecord.Empty`.         |
| Outcome     | `CompleteAsync`                                    | Set `Completed`. Update, never delete.                                                                                                                         |
| Outcome     | `RetryAsync`                                       | Set back to `Scheduled`, due after `delay`; store attempt count, last delay, and error.                                                                        |
| Outcome     | `DeadLetterAsync`                                  | Set `DeadLettered`; store the error.                                                                                                                           |
| Maintenance | `RescheduleClaimedExceedingProcessingTimeoutAsync` | Reset `InProgress` records older than `ProcessingTimeout` to `Scheduled`, incrementing the timeout count; dead-letter once it reaches `MaxProcessingTimeouts`. |
| Maintenance | `DeadLetterUnclaimedAsync`                         | Dead-letter `Scheduled` records still unclaimed past `UnclaimedTtl`.                                                                                           |
| Maintenance | `DeleteCompletedAndDeadLetteredExceedingTtlAsync`  | Delete `Completed` records past `CompletedRecordTtl` and `DeadLettered` past `DeadLetteredRecordTtl`.                                                          |

A record carries the fields the lifecycle needs: identity (`Id`, `EventId`), `EventName` and `HandlerName`, the serialized `Payload`, the `EventStatus`, scheduling and retry state (`ScheduledAt`, `RetryAttemptCount`, `RetryLastDelay`, `ClaimedAt`), audit timestamps (`CreatedAt`, `LastUpdatedAt`), the timeout counter (`ProcessingTimeoutsCount`), and `LastError`. `EventStatus` has four operational values: `Scheduled`, `InProgress`, `Completed`, and `DeadLettered`. How you store them, as columns, document fields, or hash entries, is yours to choose.

## Claiming with optimistic concurrency

When several instances run, they poll the same store. `FetchScheduledAsync()` is a plain read with no lock, so two instances can fetch the same record in the same window. The claim resolves that race: exactly one instance may move a record from `Scheduled` to `InProgress`, and the others must come away empty-handed.

`FetchScheduledAsync()` captures each candidate's `LastUpdatedAt`. That timestamp is the concurrency token. `TryClaimAsync()` succeeds only if, at the moment of the write, the record is still `Scheduled` and its `LastUpdatedAt` still matches the captured value. The winning write sets the status to `InProgress`, stamps `ClaimedAt`, and advances `LastUpdatedAt`. A racing claim then finds either a non-`Scheduled` status or a moved token, matches nothing, and returns `EventRecord.Empty`.

The check and the write must be one atomic operation in the store. A read followed by a separate write is a race: two instances can both pass the check before either writes. Reach for whatever atomic conditional write your store provides, expressed against the status and the token. For reference, the shipped backends map this to a conditional `UPDATE ... WHERE status = scheduled AND last_updated_at = token` (relational), a `FindOneAndUpdate` with the same filter (MongoDB), and an atomic Lua script (Redis).

On a successful claim, deserialize the payload (see [Serialization](#serialization)) and return the full `EventRecord`. If deserialization fails, return `EventRecord.Empty` so the event broker skips the record instead of dispatching a half-built event.

## Serialization

The core library carries no serializer. Serializing and deserializing the event payload is the backend's job, in whatever format suits the store: JSON, MessagePack, Protobuf, or raw bytes. This is also what keeps the core AOT-compatible, since a shared reflection-based serializer would not be. See [Persistent Events: Serialization](05-persistent-events.md#serialization) for the constraint this places on event types.

You serialize in two places and deserialize in one:

- **Serialize** the published event in `ScheduleAsync()` and `ScheduleDeferredAsync()`, storing the result as the record's payload.
- **Deserialize** in `TryClaimAsync()`, after the claim succeeds.

Deserialization needs the CLR type, and the stored record carries only the event name. `TryClaimAsync()` receives the `EventRegistry` for this: `GetEventType(eventName)` returns the registered type to hand to your deserializer. An unknown name, or a payload that fails to deserialize, yields `EventRecord.Empty`.

If you target AOT, your serializer must be AOT-safe. The shipped backends use System.Text.Json.

## Maintenance operations

The three maintenance methods keep the store healthy over time. The event broker runs them on its own schedule (see [Persistent Events: Maintenance loops](05-persistent-events.md#maintenance-loops)); implement each as a single set-based operation over the store, not a row-by-row loop. Their thresholds come from the `PersistentEventBrokerSettings` your backend is constructed with, compared against UTC time.

`RescheduleClaimedExceedingProcessingTimeoutAsync()` recovers records stranded by a crash. An instance that dies mid-processing leaves a record `InProgress` indefinitely. Reset every `InProgress` record claimed longer ago than `ProcessingTimeout` back to `Scheduled`, clear its `ClaimedAt`, and increment `ProcessingTimeoutsCount`. Once that count reaches `MaxProcessingTimeouts`, dead-letter the record instead of rescheduling, so one that keeps timing out cannot loop forever.

`DeadLetterUnclaimedAsync()` dead-letters `Scheduled` records still unclaimed past `UnclaimedTtl`, catching events no instance ever picked up.

`DeleteCompletedAndDeadLetteredExceedingTtlAsync()` deletes terminal records past their retention: `Completed` past `CompletedRecordTtl`, `DeadLettered` past `DeadLetteredRecordTtl`. It is the only operation that removes rows.

## Registering the backend

Wire your backend in with an extension method on `EventBrokerBuilder` that registers your settings and your `IEventStorage` implementation as keyed singletons under the broker's key. It can ship as its own NuGet package, like the built-in backends, or live in your own solution as a project or a single class.

```csharp
public static EventBrokerBuilder WithMyStorePersistence(
    this EventBrokerBuilder builder,
    Action<MyStoreSettings, PersistentEventBrokerSettings> configure)
{
    var storeSettings = new MyStoreSettings();
    var brokerSettings = new PersistentEventBrokerSettings();
    configure(storeSettings, brokerSettings);

    builder.Services
        .AddKeyedSingleton(builder.EventBrokerKey, storeSettings)
        .AddKeyedSingleton(builder.EventBrokerKey, brokerSettings)
        .AddKeyedSingleton<IEventStorage>(
            builder.EventBrokerKey,
            (sp, key) => new MyStoreStorage(
                sp.GetRequiredKeyedService<MyStoreSettings>(key),
                sp.GetRequiredKeyedService<PersistentEventBrokerSettings>(key)));

    return builder;
}
```

Registering a keyed `IEventStorage` under the broker's key is the entire switch: the event broker detects it and starts in persistent mode instead of in-memory. There is no separate flag.

From there, a custom backend is used exactly like a built-in one. The caller configures it inside `AddEventBroker()` and starts the loops with `UsePersistentEventBroker()`, as shown in [Persistent Events](05-persistent-events.md#starting-the-broker).
