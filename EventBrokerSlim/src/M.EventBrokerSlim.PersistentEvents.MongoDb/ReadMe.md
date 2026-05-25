# M.EventBrokerSlim.PersistentEvents.MongoDb

[![build](https://github.com/petar-m/EventBrokerSlim/actions/workflows/build.yml/badge.svg)](https://github.com/petar-m/EventBrokerSlim/actions)
[![NuGet](https://img.shields.io/nuget/v/M.EventBrokerSlim.PersistentEvents.MongoDb.svg)](https://www.nuget.org/packages/M.EventBrokerSlim.PersistentEvents.MongoDb)

MongoDB storage backend for [EventBrokerSlim](https://github.com/petar-m/EventBrokerSlim/blob/main/EventBrokerSlim/ReadMe.md) persistent events - durable, at-least-once event delivery that survives process restarts, with support for horizontal scale-out.

For the design rationale behind persistent events, see the [architecture document](https://github.com/petar-m/EventBrokerSlim/blob/main/EventBrokerSlim/EventBrokerSlim-Persistence-Architecture.md) and [ADRs](https://github.com/petar-m/EventBrokerSlim/tree/main/EventBrokerSlim/ADRs).

## Prerequisites

- .NET 8.0 or later
- [M.EventBrokerSlim](https://www.nuget.org/packages/M.EventBrokerSlim) (pulled automatically as a dependency)
- A running MongoDB server (4.2 or later)

## Installation

```shell
dotnet add package M.EventBrokerSlim.PersistentEvents.MongoDb
```

## Database Setup

No manual setup is required. On first use, the storage creates the configured collection and ensures the necessary indexes. The default collection name is `events` - use a unique collection name per event broker instance when sharing the same database.

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

### 4. Configure the event broker with MongoDB persistence

```csharp
serviceCollection.AddEventBroker(x => x
    .WithMaxConcurrentHandlers(3)
    .WithMongoDbPersistence((db, settings) =>
    {
        db.ConnectionString = "mongodb://localhost:27017";
        db.DatabaseName = "myapp";
        db.CollectionName = "events";
        settings.PollingInterval = TimeSpan.FromSeconds(10);
        settings.ProcessingTimeout = TimeSpan.FromMinutes(5);
    }));
```

**Alternative: supply an existing `IMongoDatabase` instance**

When the host application already manages a `MongoClient` or `IMongoDatabase` (e.g. registered via `AddMongoClient`), pass the database directly. `ConnectionString` and `DatabaseName` are ignored when an instance is provided:

```csharp
IMongoDatabase mongoDatabase = mongoClient.GetDatabase("myapp");

serviceCollection.AddEventBroker(x => x
    .WithMongoDbPersistence((db, settings) =>
    {
        db.MongoDatabase = mongoDatabase;
        db.CollectionName = "events";
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

| Property           | Default      | Description                                                                                                                   |
| ------------------ | ------------ | ----------------------------------------------------------------------------------------------------------------------------- |
| `ConnectionString` | `null`       | MongoDB connection string. **Required** unless `MongoDatabase` is supplied. Example: `"mongodb://localhost:27017"`            |
| `DatabaseName`     | `"ebs_0"`    | MongoDB database name. Ignored when `MongoDatabase` is supplied.                                                             |
| `CollectionName`   | `"events"`   | Collection name for event documents. Use a unique name per event broker instance when sharing the same database.             |
| `MongoDatabase`    | `null`       | Optional existing `IMongoDatabase` instance. When set, `ConnectionString` and `DatabaseName` are ignored.                    |

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

**Multi-instance horizontal scale-out.** MongoDB is a server-based database suitable for multi-instance deployments. The claiming mechanism uses `findOneAndUpdate` with optimistic concurrency to ensure at most one instance processes a given record under normal operation.

**MongoDB 4.2 or later required.** The maintenance loop (`RescheduleClaimedExceedingProcessingTimeoutAsync`) uses aggregation pipeline updates introduced in MongoDB 4.2.

**At-least-once delivery.** A crash after claiming a record but before completing it may cause duplicate processing. Handlers must be idempotent.

**Escaped exceptions are dead-lettered.** If an exception escapes the handler pipeline unhandled, the record is immediately dead-lettered. Handle exceptions inside the pipeline to use retries.

**Name stability.** Changing a `handlerName` or an `EventRegistry` name is a breaking change - in-flight records under the old name will never be claimed. Treat handler name changes as migrations.

**Serialization.** Events are serialized using `System.Text.Json` with camelCase property naming, no indentation, and null values omitted. Event types must be serializable under these settings.

**Not event sourcing.** The store is a delivery mechanism, not an event log. Completed records are deleted according to `CompletedRecordTtl`.

**Not a transactional outbox.** The event write to MongoDB is not atomic with the caller's own database transaction.

## Database Schema

The package stores event documents in a single MongoDB collection (default name `events`). The collection and its indexes are created automatically on first use.

### Event Document

| Field                     | Type       | Description                                                              |
| ------------------------- | ---------- | ------------------------------------------------------------------------ |
| `_id`                     | `ObjectId` | Auto-generated record identifier                                         |
| `event_id`                | `string`   | Unique identifier for the event instance (shared across fan-out records) |
| `event_name`              | `string`   | Registered event name from `EventRegistry`                               |
| `handler_name`            | `string`   | Registered handler name                                                  |
| `payload`                 | `string`   | JSON-serialized event data                                               |
| `status`                  | `int`      | 1=Scheduled, 2=InProgress, 3=Completed, 4=DeadLettered                   |
| `scheduled_at`            | `datetime` | UTC timestamp when the record becomes eligible for processing            |
| `retry_attempt_count`     | `int`      | Number of retry attempts                                                 |
| `retry_last_delay`        | `int64`    | Duration of the last retry delay in milliseconds                         |
| `claimed_at`              | `datetime` | UTC timestamp when the record was claimed for processing                 |
| `created_at`              | `datetime` | UTC timestamp when the record was created                                |
| `last_updated_at`         | `datetime` | UTC timestamp of last update (used for optimistic concurrency)           |
| `last_error`              | `string`   | Error message from the most recent failure                               |
| `processing_timeouts_count` | `int`    | Number of times processing timed out                                     |

### Indexes

| Index                          | Purpose                                                                            |
| ------------------------------ | ---------------------------------------------------------------------------------- |
| `status ASC, scheduled_at ASC` | Covers polling queries that filter by status and order by scheduled time.          |
| `status ASC, claimed_at ASC`   | Supports maintenance queries that filter `InProgress` records by `claimed_at`.     |
| `status ASC, last_updated_at ASC` | Supports retention cleanup queries for terminal statuses by `last_updated_at`.  |

## License

MIT
