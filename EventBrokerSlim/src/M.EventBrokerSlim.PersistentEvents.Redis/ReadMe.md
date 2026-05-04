# M.EventBrokerSlim.PersistentEvents.Redis  

[![build](https://github.com/petar-m/EventBrokerSlim/actions/workflows/build.yml/badge.svg)](https://github.com/petar-m/EventBrokerSlim/actions)
[![NuGet](https://img.shields.io/nuget/v/M.EventBrokerSlim.PersistentEvents.Redis.svg)](https://www.nuget.org/packages/M.EventBrokerSlim.PersistentEvents.Redis)    

Redis storage backend for [EventBrokerSlim](https://github.com/petar-m/EventBrokerSlim/blob/main/EventBrokerSlim/ReadMe.md) persistent events - durable, at-least-once event delivery that survives process restarts.

For the design rationale behind persistent events, see the [architecture document](https://github.com/petar-m/EventBrokerSlim/blob/main/EventBrokerSlim/EventBrokerSlim-Persistence-Architecture.md) and [ADRs](https://github.com/petar-m/EventBrokerSlim/tree/main/EventBrokerSlim/ADRs).

## Prerequisites

- .NET 8.0 or later  
- Redis server  
- [M.EventBrokerSlim](https://www.nuget.org/packages/M.EventBrokerSlim) (pulled automatically as a dependency)

## Installation

```shell
dotnet add package M.EventBrokerSlim.PersistentEvents.Redis
```


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

Map each event type to a stable string name. The name is stored in Redis - it must not change between deployments:

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

### 4. Configure the event broker with Redis persistence

```csharp
serviceCollection.AddEventBroker(x => x
    .WithMaxConcurrentHandlers(3)
    .WithRedisPersistence((redis, settings) =>
    {
        redis.ConnectionString = "localhost:6379";
        redis.KeyPrefix = "ebs_0";
        settings.PollingInterval = TimeSpan.FromSeconds(10);
        settings.ProcessingTimeout = TimeSpan.FromMinutes(5);
    }));
```

**Using a registered `IConnectionMultiplexer`**

If you already have a `IConnectionMultiplexer` registered in the DI container, you can reuse it instead of providing a connection string:

```csharp
serviceCollection.AddEventBroker(x => x
    .WithMaxConcurrentHandlers(3)
    .WithRedisPersistence((redis, settings) =>
    {
        redis.UseRegisteredMultiplexer = true;
        redis.KeyPrefix = "ebs_0";
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

### RedisSettings

| Property                   | Default   | Description                                                                                                                           |
| -------------------------- | --------- | ------------------------------------------------------------------------------------------------------------------------------------- |
| `ConnectionString`         | `null`    | Redis connection string (e.g. `"localhost:6379"`). **Required** unless `UseRegisteredMultiplexer` is `true`.                          |
| `UseRegisteredMultiplexer` | `false`   | When `true`, resolves an `IConnectionMultiplexer` from the DI container instead of creating a new connection from `ConnectionString`. |
| `KeyPrefix`                | `"ebs_0"` | Prefix for all Redis keys. Use a unique prefix per event broker instance when sharing the same Redis server.                          |

### PersistentEventBrokerSettings

| Property                | Default    | Description                                                                                                                               |
| ----------------------- | ---------- | ----------------------------------------------------------------------------------------------------------------------------------------- |
| `PollingInterval`       | 10 seconds | How often the poller checks for scheduled records. Shorter intervals reduce cross-instance latency at the cost of more queries when idle. |
| `ProcessingTimeout`     | 5 minutes  | In-progress records exceeding this duration are rescheduled. Must be longer than the longest expected handler execution time.             |
| `MaxProcessingTimeouts` | 10         | Maximum number of times a record can be rescheduled due to processing timeout before it is dead-lettered.                                 |
| `ScheduledBatchSize`    | 10         | Maximum number of scheduled records fetched per poll.                                                                                     |
| `UnclaimedTtl`          | 7 days     | Scheduled records not claimed within this duration (measured from `scheduled_at`) are dead-lettered.                                      |
| `CompletedRecordTtl`    | 7 days     | Completed records are deleted after this duration.                                                                                        |
| `DeadLetteredRecordTtl` | 30 days    | Dead-lettered records are deleted after this duration. Should be long enough to give operators time to inspect and act.                   |

## Important Considerations

**At-least-once delivery.** A crash after claiming a record but before completing it may cause duplicate processing. Handlers must be idempotent.

**Escaped exceptions are dead-lettered.** If an exception escapes the handler pipeline unhandled, the record is immediately dead-lettered - `IRetryPolicy` is not consulted. Handle exceptions inside the pipeline to use retries.

**Name stability.** Changing a `handlerName` or an `EventRegistry` name is a breaking change - in-flight records under the old name will never be claimed. Treat name changes as migrations.

**Serialization.** Events are serialized using `System.Text.Json` with camelCase property naming, no indentation, and null values omitted. Event types must be serializable under these settings.

**Not event sourcing.** The store is a delivery mechanism, not an event log. Completed records are deleted according to `CompletedRecordTtl`.

**Not a transactional outbox.** The event write to Redis is not atomic with the caller's own database transaction.

**Dead-letter monitoring.** Records land in a dead-letter state when the retry policy is exhausted, when a handler abandons the event, or when an exception escapes unhandled. Dead-lettered records are not retried automatically - monitoring and tooling for inspection and requeue is necessary for production use.

## Redis Data Model

All data is stored under the configured `KeyPrefix` (default `ebs_0`). The `{KeyPrefix}` portion is wrapped in braces in the actual keys to enable Redis hash-tag-based slot routing for cluster compatibility.

### Event Records

Each event record is stored as a Redis Hash at key `{KeyPrefix}:evt:{id}` where `id` is a GUID.

| Field                       | Description                                                                            |
| --------------------------- | -------------------------------------------------------------------------------------- |
| `event_id`                  | Unique identifier for the event instance (shared across fan-out records)               |
| `event_name`                | Registered event name from `EventRegistry`                                             |
| `handler_name`              | Registered handler name                                                                |
| `payload`                   | JSON-serialized event data                                                             |
| `status`                    | 1=Scheduled, 2=InProgress, 3=Completed, 4=DeadLettered                                 |
| `scheduled_at`              | Unix timestamp (ms) when the record becomes eligible for processing                    |
| `retry_attempt_count`       | Number of retry attempts                                                               |
| `retry_last_delay`          | Duration of the last retry delay (ms)                                                  |
| `claimed_at`                | Unix timestamp (ms) when the record was claimed for processing                         |
| `created_at`                | Unix timestamp (ms) when the record was created                                        |
| `last_updated_at`           | Unix timestamp (ms) when the record was last updated (used for optimistic concurrency) |
| `last_error`                | Error message from the most recent failure                                             |
| `processing_timeouts_count` | Number of times processing timed out                                                   |

### Sorted Set Indexes

Four sorted sets partition records by status, scored by timestamp for efficient range queries:

| Key                             | Purpose                                                            |
| ------------------------------- | ------------------------------------------------------------------ |
| `{KeyPrefix}:idx:scheduled`     | Records eligible for dispatch, scored by `scheduled_at`            |
| `{KeyPrefix}:idx:in_progress`   | Records currently being processed, scored by `claimed_at`          |
| `{KeyPrefix}:idx:completed`     | Finished records awaiting TTL cleanup, scored by `last_updated_at` |
| `{KeyPrefix}:idx:dead_lettered` | Failed records awaiting inspection, scored by `last_updated_at`    |

### Atomicity

All state transitions (schedule, claim, complete, retry, dead-letter, timeout reschedule, cleanup) are executed as atomic Lua scripts, ensuring consistency even under concurrent access.

## License

MIT
