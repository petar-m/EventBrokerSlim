# M.EventBrokerSlim.PersistentEvents.PostgreSql  

[![build](https://github.com/petar-m/EventBrokerSlim/actions/workflows/build.yml/badge.svg)](https://github.com/petar-m/EventBrokerSlim/actions)
[![NuGet](https://img.shields.io/nuget/v/M.EventBrokerSlim.PersistentEvents.PostgreSql.svg)](https://www.nuget.org/packages/M.EventBrokerSlim.PersistentEvents.PostgreSql)    

PostgreSQL storage backend for [EventBrokerSlim](https://github.com/petar-m/EventBrokerSlim/blob/main/EventBrokerSlim/ReadMe.md) persistent events - durable, at-least-once event delivery that survives process restarts.

For the design rationale behind persistent events, see the [architecture document](https://github.com/petar-m/EventBrokerSlim/blob/main/EventBrokerSlim/EventBrokerSlim-Persistence-Architecture.md) and [ADRs](https://github.com/petar-m/EventBrokerSlim/tree/main/EventBrokerSlim/ADRs).

## Prerequisites

- .NET 8.0 or later  
- PostgreSQL server  
- [M.EventBrokerSlim](https://www.nuget.org/packages/M.EventBrokerSlim) (pulled automatically as a dependency)

## Installation

```shell
dotnet add package M.EventBrokerSlim.PersistentEvents.PostgreSql
```

## Database Setup

The package requires a schema, table, sequence, and indexes in your PostgreSQL database. There are two ways to set them up:

### Option 1: Programmatic (development / simple deployments)

Call `CreateEventsTable()` on a `DatabaseSettings` instance:

```csharp
var databaseSettings = new DatabaseSettings
{
    ConnectionString = "Host=localhost;Database=mydb;Username=myuser;Password=mypassword",
    Schema = "ebs_0"
};

databaseSettings.CreateEventsTable();
```

The call is idempotent - it uses `CREATE ... IF NOT EXISTS` and is safe to run on every startup in development.

> [!WARNING]
> `CreateEventsTable()` requires DDL permissions (create schemas, tables, sequences, and indexes). Production applications should typically run this as part of a deployment or migration process, not at application startup.

### Option 2: Manual SQL script

Apply the `initialize_db.sql` script included in the package source. The default schema name is `ebs_0` - replace it with a unique name if running multiple event broker instances against the same database.

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

Instead of a class, define the handler as a delegate pipeline:

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
            // optionally retry
            retryPolicy.RetryAfter(TimeSpan.FromSeconds(5));
        }
    })
    .Execute(static async (OrderPlaced @event, ISomeService service, CancellationToken ct) =>
    {
        // process the event - must be idempotent
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

Only handlers registered with a `handlerName` have their names included in fan-out - when an event is published, one storage record is created per registered handler name:

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

Register the delegate pipeline with a persistent name:

```csharp
serviceCollection.AddEventHandlerPipeline<OrderPlaced>(pipeline,
    handlerName: "OrderPlacedHandler");
```

Or using the options API:

```csharp
serviceCollection.AddEventHandlerPipeline<OrderPlaced>(pipeline, o => o
    .WithHandlerName("OrderPlacedHandler"));
```

### 4. Configure the event broker with PostgreSQL persistence

```csharp
serviceCollection.AddEventBroker(x => x
    .WithMaxConcurrentHandlers(3)
    .WithPostgreSqlPersistence((db, settings) =>
    {
        db.ConnectionString = "Host=localhost;Database=mydb;Username=myuser;Password=mypassword";
        db.Schema = "ebs_0";
        settings.PollingInterval = TimeSpan.FromSeconds(10);
        settings.ProcessingTimeout = TimeSpan.FromMinutes(5);
    }));
```

### 5. Start the persistent event broker

```csharp
var serviceProvider = serviceCollection.BuildServiceProvider();
serviceProvider.UsePersistentEventBroker(throwOnValidationErrors: true);
```

On startup, validation checks that:
- Every handler with a `handlerName` has its event type registered in `EventRegistry`  
- Every event in `EventRegistry` has at least one named handler (including `NullPipeline` registrations)  

Set `throwOnValidationErrors: true` for strict mode (default logs warnings).

### 6. Publish events

`IEventBroker` is registered in the DI container and can be injected where needed:

```csharp
await eventBroker.Publish(new OrderPlaced("order-123", 49.99m));
```

## Configuration Reference

### DatabaseSettings

| Property | Default | Description |
|---|---|---|
| `ConnectionString` | `null` | PostgreSQL connection string. **Required.** |
| `Schema` | `"ebs_0"` | Database schema for the events table. Use a unique schema per event broker instance when sharing the same database. |

### PersistentEventBrokerSettings

| Property | Default | Description |
|---|---|---|
| `PollingInterval` | 10 seconds | How often the poller checks for scheduled records. Shorter intervals reduce cross-instance latency at the cost of more queries when idle. |
| `ProcessingTimeout` | 5 minutes | In-progress records exceeding this duration are rescheduled. Must be longer than the longest expected handler execution time. |
| `MaxProcessingTimeouts` | 10 | Maximum number of times a record can be rescheduled due to processing timeout before it is dead-lettered. |
| `ScheduledBatchSize` | 10 | Maximum number of scheduled records fetched per poll. |
| `UnclaimedTtl` | 7 days | Scheduled records not claimed within this duration (measured from `scheduled_at`) are dead-lettered. |
| `CompletedRecordTtl` | 7 days | Completed records are deleted after this duration. |
| `DeadLetteredRecordTtl` | 30 days | Dead-lettered records are deleted after this duration. Should be long enough to give operators time to inspect and act. |

## Important Considerations

**At-least-once delivery.** A crash after claiming a record but before completing it may cause duplicate processing. Handlers must be idempotent.

**Escaped exceptions are dead-lettered.** If an exception escapes the handler pipeline unhandled, the record is immediately dead-lettered - `IRetryPolicy` is not consulted. Handle exceptions inside the pipeline to use retries.

**Name stability.** Changing a `handlerName` or an `EventRegistry` name is a breaking change - in-flight records under the old name will never be claimed. Treat name changes as migrations.

**Serialization.** Events are serialized using `System.Text.Json` with camelCase property naming, no indentation, and null values omitted. Event types must be serializable under these settings.

**Not event sourcing.** The store is a delivery mechanism, not an event log. Completed records are deleted according to `CompletedRecordTtl`.

**Not a transactional outbox.** The event write to PostgreSQL is not atomic with the caller's own database transaction.

**Dead-letter monitoring.** Records land in a dead-letter state when the retry policy is exhausted, when a handler abandons the event, or when an exception escapes unhandled. Dead-lettered records are not retried automatically - monitoring and tooling for inspection and requeue is necessary for production use.

## Database Schema

The package creates the following schema (default name `ebs_0`):

### Events Table

| Column | Type | Description |
|---|---|---|
| `id` | `BIGINT` (PK) | Auto-incrementing record identifier |
| `event_id` | `TEXT` | Unique identifier for the event instance (shared across fan-out records) |
| `event_name` | `TEXT` | Registered event name from `EventRegistry` |
| `handler_name` | `TEXT` | Registered handler name |
| `payload` | `TEXT` | JSON-serialized event data |
| `status` | `INT` | 1=Scheduled, 2=InProgress, 3=Completed, 4=DeadLettered |
| `scheduled_at` | `TIMESTAMPTZ` | When the record becomes eligible for processing |
| `retry_attempt_count` | `INTEGER` | Number of retry attempts |
| `retry_last_delay` | `INTERVAL` | Duration of the last retry delay |
| `claimed_at` | `TIMESTAMPTZ` | When the record was claimed for processing |
| `created_at` | `TIMESTAMPTZ` | When the record was created |
| `last_updated_at` | `TIMESTAMPTZ` | When the record was last updated (used for optimistic concurrency) |
| `last_error` | `TEXT` | Error message from the most recent failure |
| `processing_timeouts_count` | `INTEGER` | Number of times processing timed out |

### Indexes

| Index | Purpose |
|---|---|
| **Polling** (partial, `status=1`) | Covers the high-frequency polling query. Includes `id`, `last_updated_at`, `event_name`, `handler_name` for index-only scans. |
| **Timeout** (partial, `status=2`) | Enables efficient timeout detection for in-progress records. |
| **Cleanup** (partial, `status IN (3,4)`) | Supports retention-based deletion of completed and dead-lettered records. |

## License

MIT
