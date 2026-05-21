# M.EventBrokerSlim.PersistentEvents.LiteDb

[![build](https://github.com/petar-m/EventBrokerSlim/actions/workflows/build.yml/badge.svg)](https://github.com/petar-m/EventBrokerSlim/actions)
[![NuGet](https://img.shields.io/nuget/v/M.EventBrokerSlim.PersistentEvents.LiteDb.svg)](https://www.nuget.org/packages/M.EventBrokerSlim.PersistentEvents.LiteDb)

LiteDB storage backend for [EventBrokerSlim](https://github.com/petar-m/EventBrokerSlim/blob/main/EventBrokerSlim/ReadMe.md) persistent events - durable, at-least-once event delivery that survives process restarts. No database server required.

For the design rationale behind persistent events, see the [architecture document](https://github.com/petar-m/EventBrokerSlim/blob/main/EventBrokerSlim/EventBrokerSlim-Persistence-Architecture.md) and [ADRs](https://github.com/petar-m/EventBrokerSlim/tree/main/EventBrokerSlim/ADRs).

## Prerequisites

- .NET 8.0 or later
- [M.EventBrokerSlim](https://www.nuget.org/packages/M.EventBrokerSlim) (pulled automatically as a dependency)
- LiteDB is embedded - no database server installation required

## Installation

```shell
dotnet add package M.EventBrokerSlim.PersistentEvents.LiteDb
```

## Database Setup

No manual setup is required. On first use, the storage opens (or creates) the LiteDB file specified by the connection string, retrieves the configured collection, and ensures the necessary indexes. The default collection name is `events` - use a unique collection name per event broker instance when sharing the same database file.

## Quick Start

### 1. Define events and handlers

```csharp
public record OrderPlaced(string OrderId, decimal Amount);

public class OrderPlacedHandler : IEventHandler<OrderPlaced>
{
    public async Task Handle(OrderPlaced @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken)
    {
        // process the event - must be idempotent
    }

    public async Task OnError(Exception exception, OrderPlaced @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken)
    {
        // optionally retry
        retryPolicy.RetryAfter(TimeSpan.FromSeconds(5));
    }
}
```

**Alternative: delegate handler**

```csharp
public record OrderPlaced(string OrderId, decimal Amount);

IPipeline pipeline = PipelineBuilder.Create()
    .NewPipeline()
    .Execute(static async (IRetryPolicy retryPolicy, INext next) =>
    {
        try
        {
            await next.RunAsync();
        }
        catch(Exception exception)
        {
            retryPolicy.RetryAfter(TimeSpan.FromSeconds(5));
        }
    })
    .Execute(static async (OrderPlaced @event, ISomeService service, CancellationToken ct) =>
    {
        await service.ProcessOrder(@event, ct);
    })
    .Build()
    .Pipelines[0];
```

### 2. Register the event registry

Map each event type to a stable string name. The name is stored in the database - it must not change between deployments:

```csharp
var eventRegistry = new EventRegistry()
    .Add<OrderPlaced>("OrderPlaced");

serviceCollection.AddSingleton(eventRegistry);
```

### 3. Register handlers with persistent names

Only handlers registered with a `handlerName` participate in fan-out:

```csharp
serviceCollection.AddTransientEventHandler<OrderPlaced, OrderPlacedHandler>(
    handlerName: "OrderPlacedHandler");
```

Or using the options API:

```csharp
serviceCollection.AddTransientEventHandler<OrderPlaced, OrderPlacedHandler>(o => o
    .WithHandlerName("OrderPlacedHandler"));
```

**Alternative: delegate handler**

```csharp
serviceCollection.AddEventHandlerPipeline<OrderPlaced>(pipeline,
    handlerName: "OrderPlacedHandler");
```

### 4. Configure the event broker with LiteDB persistence

```csharp
serviceCollection.AddEventBroker(x => x
    .WithMaxConcurrentHandlers(3)
    .WithLiteDbPersistence((db, settings) =>
    {
        db.ConnectionString = "Filename=events.db";
        db.Collection = "events";
        settings.PollingInterval = TimeSpan.FromSeconds(10);
        settings.ProcessingTimeout = TimeSpan.FromMinutes(5);
    }));
```

**Alternative: supply an existing `LiteDatabase` instance**

When the host application already manages a `LiteDatabase` (for shared lifetime, custom `BsonMapper`, or shared connection pooling), pass it directly. The connection string is ignored when an instance is provided:

```csharp
var liteDb = new LiteDatabase("Filename=events.db");

serviceCollection.AddEventBroker(x => x
    .WithLiteDbPersistence((db, settings) =>
    {
        db.LiteDbInstance = liteDb;
        db.Collection = "events";
    }));
```

### 5. Start the persistent event broker

```csharp
var serviceProvider = serviceCollection.BuildServiceProvider();
serviceProvider.UsePersistentEventBroker(throwOnValidationErrors: true);
```

### 6. Publish events

```csharp
await eventBroker.Publish(new OrderPlaced("order-123", 49.99m));
```

## Configuration Reference

### DatabaseSettings

| Property           | Default    | Description                                                                                                            |
| ------------------ | ---------- | ---------------------------------------------------------------------------------------------------------------------- |
| `ConnectionString` | `null`     | LiteDB connection string. **Required** unless `LiteDbInstance` is supplied. Example: `"Filename=events.db"`            |
| `Collection`       | `"events"` | Collection name for event documents. Use a unique name per event broker instance when sharing the same database file. |
| `LiteDbInstance`   | `null`     | Optional existing `LiteDatabase` instance. When set, `ConnectionString` is ignored and the instance is reused as-is.   |

### PersistentEventBrokerSettings

| Property                | Default    | Description                                                                                                                   |
| ----------------------- | ---------- | ----------------------------------------------------------------------------------------------------------------------------- |
| `PollingInterval`       | 10 seconds | How often the poller checks for scheduled records.                                                                            |
| `ProcessingTimeout`     | 5 minutes  | In-progress records exceeding this duration are rescheduled. Must be longer than the longest expected handler execution time. |
| `MaxProcessingTimeouts` | 10         | Maximum number of times a record can be rescheduled due to processing timeout before it is dead-lettered.                     |
| `ScheduledBatchSize`    | 10         | Maximum number of scheduled records fetched per poll.                                                                         |
| `UnclaimedTtl`          | 7 days     | Scheduled records not claimed within this duration (measured from `scheduled_at`) are dead-lettered.                          |
| `CompletedRecordTtl`    | 7 days     | Completed records are deleted after this duration.                                                                            |
| `DeadLetteredRecordTtl` | 30 days    | Dead-lettered records are deleted after this duration.                                                                        |

## Important Considerations

**Single-instance scope.** LiteDB is an embedded database. It targets single-process deployments where durability (crash survival) is needed but multi-instance horizontal scale-out is not required. For multi-instance deployments, consider a server-based backend.

**At-least-once delivery.** A crash after claiming a record but before completing it may cause duplicate processing. Handlers must be idempotent.

**Snapshot isolation.** LiteDB transactions use snapshot isolation, not serializable isolation. The storage relies on optimistic concurrency (filtered `UpdateMany` on the `last_updated_at` field) to make claim and state transitions safe under concurrent access within a single process.

**Escaped exceptions are dead-lettered.** If an exception escapes the handler pipeline unhandled, the record is immediately dead-lettered. Handle exceptions inside the pipeline to use retries.

**Name stability.** Changing a `handlerName` or an `EventRegistry` name is a breaking change - in-flight records under the old name will never be claimed.

**Serialization.** Events are serialized using `System.Text.Json` with camelCase property naming, no indentation, and null values omitted. Event types must be serializable under these settings.

**Not event sourcing.** The store is a delivery mechanism, not an event log. Completed records are deleted according to `CompletedRecordTtl`.

**Not a transactional outbox.** The event write to LiteDB is not atomic with the caller's own database transaction.

## Database Schema

The package stores event documents in a single LiteDB collection (default name `events`). The collection and its indexes are created automatically on first use.

### Event Document

| Field                     | Type       | Description                                                              |
| ------------------------- | ---------- | ------------------------------------------------------------------------ |
| `Id`                      | `INT64` (PK) | Auto-incrementing record identifier                                    |
| `EventId`                 | `STRING`   | Unique identifier for the event instance (shared across fan-out records) |
| `EventName`               | `STRING`   | Registered event name from `EventRegistry`                               |
| `HandlerName`             | `STRING`   | Registered handler name                                                  |
| `Payload`                 | `STRING`   | JSON-serialized event data                                               |
| `Status`                  | `INT`      | 1=Scheduled, 2=InProgress, 3=Completed, 4=DeadLettered                   |
| `ScheduledAt`             | `DATETIME` | UTC timestamp when the record becomes eligible for processing            |
| `RetryAttemptCount`       | `INT`      | Number of retry attempts                                                 |
| `RetryLastDelay`          | `INT64`    | Duration of the last retry delay in milliseconds                         |
| `ClaimedAt`               | `DATETIME` | UTC timestamp when the record was claimed for processing                 |
| `CreatedAt`               | `DATETIME` | UTC timestamp when the record was created                                |
| `LastUpdatedAt`           | `DATETIME` | UTC timestamp of last update (used for optimistic concurrency)           |
| `LastError`               | `STRING`   | Error message from the most recent failure                               |
| `ProcessingTimeoutsCount` | `INT`      | Number of times processing timed out                                     |

### Indexes

| Index             | Purpose                                                                            |
| ----------------- | ---------------------------------------------------------------------------------- |
| `Status`          | Covers polling, timeout detection, and cleanup queries that filter by status.      |
| `ScheduledAt`     | Orders scheduled records by eligibility time for the polling query.                |
| `ClaimedAt`       | Supports efficient identification of in-progress records past `ProcessingTimeout`. |
| `LastUpdatedAt`   | Supports retention-based deletion of completed and dead-lettered records.          |

## License

MIT
