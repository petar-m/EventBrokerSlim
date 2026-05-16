# M.EventBrokerSlim.PersistentEvents.Sqlite

[![build](https://github.com/petar-m/EventBrokerSlim/actions/workflows/build.yml/badge.svg)](https://github.com/petar-m/EventBrokerSlim/actions)
[![NuGet](https://img.shields.io/nuget/v/M.EventBrokerSlim.PersistentEvents.Sqlite.svg)](https://www.nuget.org/packages/M.EventBrokerSlim.PersistentEvents.Sqlite)

SQLite storage backend for [EventBrokerSlim](https://github.com/petar-m/EventBrokerSlim/blob/main/EventBrokerSlim/ReadMe.md) persistent events - durable, at-least-once event delivery that survives process restarts. No database server required.

For the design rationale behind persistent events, see the [architecture document](https://github.com/petar-m/EventBrokerSlim/blob/main/EventBrokerSlim/EventBrokerSlim-Persistence-Architecture.md) and [ADRs](https://github.com/petar-m/EventBrokerSlim/tree/main/EventBrokerSlim/ADRs).

## Prerequisites

- .NET 8.0 or later
- [M.EventBrokerSlim](https://www.nuget.org/packages/M.EventBrokerSlim) (pulled automatically as a dependency)
- SQLite is embedded — no database server installation required

## Installation

```shell
dotnet add package M.EventBrokerSlim.PersistentEvents.Sqlite
```

## Database Setup

The package requires a table and indexes in your SQLite database file. There are two ways to set them up:

### Option 1: Programmatic

Call `CreateEventsTable()` on a `DatabaseSettings` instance:

```csharp
var databaseSettings = new DatabaseSettings
{
    ConnectionString = "Data Source=events.db",
    Table = "events"
};

databaseSettings.CreateEventsTable();
```

The call is idempotent - it uses `CREATE TABLE IF NOT EXISTS` and is safe to run on every startup in development. It also enables WAL (Write-Ahead Logging) mode for better concurrent read/write performance.


### Option 2: Manual SQL script

Apply the `initialize_db.sql` script included in the package source. The default table name is `events` - replace it with a unique name if running multiple event broker instances against the same database file.

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

### 4. Configure the event broker with SQLite persistence

```csharp
serviceCollection.AddEventBroker(x => x
    .WithMaxConcurrentHandlers(3)
    .WithSqlitePersistence((db, settings) =>
    {
        db.ConnectionString = "Data Source=events.db";
        db.Table = "events";
        db.CreateEventsTable();
        settings.PollingInterval = TimeSpan.FromSeconds(10);
        settings.ProcessingTimeout = TimeSpan.FromMinutes(5);
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

| Property           | Default    | Description                                                                                                    |
| ------------------ | ---------- | -------------------------------------------------------------------------------------------------------------- |
| `ConnectionString` | `null`     | SQLite connection string. **Required.** Example: `"Data Source=events.db"`                                     |
| `Table`            | `"events"` | Table name for event records. Use a unique name per event broker instance when sharing the same database file. |

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

**Single-instance scope.** SQLite is an embedded database. It targets single-process deployments where durability (crash survival) is needed but multi-instance horizontal scale-out is not required. For multi-instance deployments, consider suitable backend.

**At-least-once delivery.** A crash after claiming a record but before completing it may cause duplicate processing. Handlers must be idempotent.

**WAL mode.** `CreateEventsTable()` enables WAL (Write-Ahead Logging) journal mode, which allows concurrent reads while writing. This is important for the polling loop and publisher operating concurrently.

**Escaped exceptions are dead-lettered.** If an exception escapes the handler pipeline unhandled, the record is immediately dead-lettered. Handle exceptions inside the pipeline to use retries.

**Name stability.** Changing a `handlerName` or an `EventRegistry` name is a breaking change - in-flight records under the old name will never be claimed.

**Serialization.** Events are serialized using `System.Text.Json` with camelCase property naming, no indentation, and null values omitted. Event types must be serializable under these settings.

**Not event sourcing.** The store is a delivery mechanism, not an event log. Completed records are deleted according to `CompletedRecordTtl`.

**Not a transactional outbox.** The event write to SQLite is not atomic with the caller's own database transaction.

## Database Schema

The package creates the following schema (default table name `events`):

### Events Table

| Column                      | Type           | Description                                                              |
| --------------------------- | -------------- | ------------------------------------------------------------------------ |
| `id`                        | `INTEGER` (PK) | Auto-incrementing record identifier                                      |
| `event_id`                  | `TEXT`         | Unique identifier for the event instance (shared across fan-out records) |
| `event_name`                | `TEXT`         | Registered event name from `EventRegistry`                               |
| `handler_name`              | `TEXT`         | Registered handler name                                                  |
| `payload`                   | `TEXT`         | JSON-serialized event data                                               |
| `status`                    | `INTEGER`      | 1=Scheduled, 2=InProgress, 3=Completed, 4=DeadLettered                   |
| `scheduled_at`              | `TEXT`         | ISO 8601 UTC timestamp when the record becomes eligible for processing   |
| `retry_attempt_count`       | `INTEGER`      | Number of retry attempts                                                 |
| `retry_last_delay`          | `INTEGER`      | Duration of the last retry delay in milliseconds                         |
| `claimed_at`                | `TEXT`         | ISO 8601 UTC timestamp when the record was claimed for processing        |
| `created_at`                | `TEXT`         | ISO 8601 UTC timestamp when the record was created                       |
| `last_updated_at`           | `TEXT`         | ISO 8601 UTC timestamp of last update (used for optimistic concurrency)  |
| `last_error`                | `TEXT`         | Error message from the most recent failure                               |
| `processing_timeouts_count` | `INTEGER`      | Number of times processing timed out                                     |

### Indexes

| Index                                    | Purpose                                                                  |
| ---------------------------------------- | ------------------------------------------------------------------------ |
| **Polling** (partial, `status=1`)        | Covers the high-frequency polling query                                  |
| **Timeout** (partial, `status=2`)        | Enables efficient timeout detection for in-progress records              |
| **Cleanup** (partial, `status IN (3,4)`) | Supports retention-based deletion of completed and dead-lettered records |

## License

MIT
