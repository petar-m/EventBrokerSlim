# EventBrokerSlim  
  

[![build](https://github.com/petar-m/EventBrokerSlim/actions/workflows/build.yml/badge.svg)](https://github.com/petar-m/EventBrokerSlim/actions)
[![NuGet](https://img.shields.io/nuget/v/M.EventBrokerSlim.svg)](https://www.nuget.org/packages/M.EventBrokerSlim)    

An implementation of broadcasting events in a fire-and-forget style.  

Features:  
- in-memory, in-process
- publishing is *Fire and Forget* style  
- events don't have to implement specific interface  
- event handlers are executed on `ThreadPool` threads  
- the number of concurrent handlers running can be limited  
- built-in retry option
- tightly integrated with `Microsoft.Extensions.DependencyInjection`
- each handler is resolved and executed in a new DI container scope
- event handlers can be a [pipeline](https://github.com/petar-m/EventBrokerSlim/blob/main/FuncPipeline/ReadMe.md) of delegates  
- dynamic adding and removing of delegate event handler pipelines  
- multiple independent event broker instances in the same process
- optional [persistent events](#persistent-events) with pluggable storage backends

# How does it work

Implement an event handler by implementing `IEventHandler<TEvent>` interface:

```csharp
public record SomeEvent(string Message);

public class SomeEventHandler : IEventHandler<SomeEvent>
{
    // Inject services from DI container
    public SomeEventHandler()
    {
    }

    public async Task Handle(SomeEvent @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken)
    {
        // process the event
    }

    public async Task OnError(Exception exception, SomeEvent @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken)
    {
        // called on unhandled exception from Handle 
        // optionally use retryPolicy.RetryAfter(TimeSpan)
    }
}
```  

or create `IPipeline` of delegates to handle the event: 

```csharp
IPipeline pipeline = PipelineBuilder.Create()
      .NewPipeline()
      .Execute(static async (SomeEvent someEvent, ISomeService service, CancellationToken cancellationToken) =>
      {
          await service.DoSomething(someEvent, cancellationToken);
      })
      .Build()
      .Pipelines[0];
```  

Add event broker to DI container using `AddEventBroker` extension method and register handlers:

```csharp
serviceCollection
    .AddEventBroker()
    .AddTransientEventHandler<SomeEvent, SomeEventHandler>()
    .AddEventHandlerPipeline<SomeEvent>(pipeline);
```

Inject `IEventBroker` and publish events:

```csharp
class MyClass
{
    private readonly IEventBroker _eventBroker;

    public MyClass(IEventBroker eventBroker)
    {
        _eventBroker = eventBroker;
    }
    
    public async Task DoSomething()
    {
        var someEvent = new SomeEvent("Something happened");
        await _eventBroker.Publish(someEvent);
    }
}
```

# Overview  

`EventBroker` uses `System.Threading.Channels.Channel<T>` to decouple producers from consumers.  

There are no limits for publishers. Publishing is as fast as writing an event to a channel.  

Event handlers are resolved by event type in a new DI scope which is disposed after the handler completes. Each handler execution is scheduled on the `ThreadPool` without blocking the producer. No more than configured maximum handlers run concurrently.
  
```mermaid
graph LR;

subgraph "unlimited producers"
    event1["event"]
    event2["event"] 
    event3["event"]
end

subgraph "event broker"
    publish["publish"]
    
    subgraph "channel"
        events(["events"])
    end

    event1 --> publish
    event2 --> publish
    event3 --> publish

    publish --> events

    subgraph "single consumer"
        consumer["resolve handlers"]
    end

    events --> consumer

    subgraph "limited concurrent handlers"
        handler1["handle(event)"]
        handler2["handle(event)"]
    end

    consumer --> handler1
    consumer --> handler2
end

```

# Details

## Events

Events can be of any type. A good practice for an event is to be immutable - it may be processed by multiple handlers in different threads.  

## Event Handlers

Event handlers can be specified in two ways:
- By implementing `IEventHandler<TEvent>` interface and registering the implementation in the DI container.
- By building an `IPipeline` of delegates and registering it in the DI container.  

Both approaches can be used side by side, even for the same event. No matter how handlers are specified, a new DI container scope is created for each event handler. Every event handler is scheduled for execution on the `ThreadPool` without blocking the producer.  

### Event Handlers Implementing `IEventHandler<TEvent>`  

When event of type `TEvent` is published, `EventBroker` will resolve each `IEventHandler<TEvent>` implementation from a dedicated scope. This means that additional dependencies can be injected via the handler constructor, also resolved from the same scope.  

The parameters of `IEventHandler<TEvent>` methods are managed by `EventBroker`.  
```csharp
Task Handle(TEvent @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken);

Task OnError(Exception exception, TEvent @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken);
```
- `TEvent` - the instance of the published event.  
- `IRetryPolicy` - the instance of the retry policy for the handler (see [Retries](#retries) section).
- `CancellationToken` - the `EventBroker` cancellation token.
- `Exception` - exception thrown from `Handle`.

Since event handlers are executed on the `ThreadPool`, there is nowhere to propagate unhandled exceptions.  
An exception thrown from `Handle` method is caught and passed to `OnError` method of the same handler instance (may be on another thread however).  
An exception thrown from `OnError` is handled and swallowed and potentially logged (see [Logging](#logging) section).  

### Delegate Event Handlers

EventBroker uses the [FuncPipeline](https://github.com/petar-m/EventBrokerSlim/blob/main/FuncPipeline/ReadMe.md) library for creating and executing a pipeline of delegates for a given event.

```csharp
IPipeline pipeline = PipelineBuilder.Create()
      .NewPipeline()
      .Execute(static async (ILogger logger, INext next) =>
      {
          try
          {
             await next.RunAsync();
          }
          catch(Exception exception)
          {
             logger.LogError(exception);
          }
      })      
      .Execute(static async (SomeEvent someEvent, ISomeService someService, CancellationToken ct) =>
      {
          await someService.DoSomething(ct);
      })
      .Build()
      .Pipelines[0];

serviceCollection.AddEventHandlerPipeline<SomeEvent>(pipeline);
```

All delegate parameters are resolved from DI container scope and passed when the delegate is invoked.  
There are optional parameters available out-of-the-box:
- `TEvent` - an instance of the event being handled. Should match the type of the event the delegate was registered for.
- `IRetryPolicy` - the instance of the retry policy for the handler (see [Retries](#retries) section).
- `CancellationToken` - the `EventBroker` cancellation token. 
- `INext` - used to call the next delegate in the pipeline.

Delegate handlers do not provide special exception handling. Exception caused by resolving services or unhandled exception during execution will be handled and swallowed and potentially logged (see [Logging](#logging) section).  

### Dynamic Delegate Event Handlers 

Delegate handlers can be added or removed after DI container was built. Dynamic delegate handlers are `IPipeline` instances. 

`IServiceCollection.AddEventBroker()` registers `IDynamicEventHandlers`, used for managing handlers. Adding a handler returns `IDynamicHandlerClaimTicket`, used to remove the handler.

```csharp
public class DynamicEventHandlerExample : IDisposable
{
    private readonly IDynamicEventHandlers _dynamicEventHandlers;
    private readonly List<IDynamicHandlerClaimTicket> _claimTickets = new();

    public DynamicEventHandlerExample(IDynamicEventHandlers dynamicEventHandlers)
    {
        _dynamicEventHandlers = dynamicEventHandlers;

        // Define two handlers for different events
        var builder = PipelineBuilder.Create()
            .NewPipeline()
            .Execute<Event1, IRetryPolicy, ISomeService>(HandleEvent1)
            .Build()
            .NewPipeline()
            .Execute<Event2>(HandleEvent2)
            .Build();

        // Register with the event broker and keep a claim ticket
        var claimTicket = _dynamicEventHandlers.Add<Event1>(builder.Pipelines[0]);
        _claimTickets.Add(claimTicket);

        claimTicket = _dynamicEventHandlers.Add<Event2>(builder.Pipelines[1]);
        _claimTickets.Add(claimTicket);
    }

    // All delegate features are available, including injecting services registered in DI
    private async Task HandleEvent1(Event1 event1, IRetryPolicy retryPolicy, ISomeService someService)
    {
        // event processing 
    }

    private async Task HandleEvent2(Event2 event2)
    {
        // event processing 
    }

    public void Dispose()
    {
        // Remove both event handlers using the IDynamicHandlerClaimTicket
        _dynamicEventHandlers.RemoveRange(_claimTickets);
    }
}
```
> [!IMPORTANT]
> Make sure handlers are removed if containing classes are ephemeral. 

## DI Configuration  

`EventBroker` is depending on `Microsoft.Extensions.DependencyInjection` container for resolving event handlers and their dependencies. It guarantees that each handler is resolved in a new scope, disposed after the handler completes. There can be multiple handlers for the same event.    

`EventBroker` is configured with `AddEventBroker` extension method of `IServiceCollection` using a configuration delegate.  

```csharp
services.AddEventBroker(x => x.WithMaxConcurrentHandlers(3)
                              .DisableMissingHandlerWarningLog());
```  

- `WithMaxConcurrentHandlers` defines how many handlers can run at the same time. Default is 2.  

- `DisableMissingHandlerWarningLog` suppresses logging warning when there is no handler found for event.  

`AddKeyedEventBroker` allows registering independent event broker instance. Note that all handlers must be registered with the same key as the event broker.

### Handlers Implementing `IEventHandler<TEvent>`

Event handlers are registered by the event type and a corresponding `IEventHandler` implementation as transient, scoped, or singleton.

```csharp
serviceCollection
   .AddTransientEventHandler<TEvent1, THandler1>()  
   .AddScopedEventHandler<TEvent2, THandler2>()
   .AddSingletonEventHandler<TEvent3, THandler3>()
```  


The order of calls to `AddEventBroker` and `Add*EventHandler` does not matter. 

> [!WARNING]
> Handlers **not** registered using `Add*EventHandler<TEvent, THandler>` will be **ignored** by `EventBroker`.  
> Each handler needs to be resolved individually, thus the methods do a keyed registration with unique keys, created and tracked internally.  

> [!NOTE]
> `IEventHandler<TEvent>` registrations are internally converted to `IPipeline`.

 
### Delegate Handlers

Delegate event handlers are registered by `IServiceCollection.AddEventHandlerPipeline<TEvent>()` extension method. It will internally configure `IPipeline.ServiceScopeFactory` for each registered pipeline. A pipeline is always registered as singleton.  

```csharp
IPipeline pipeline = PipelineBuilder.Create()...;

serviceCollection.AddEventHandlerPipeline<TEvent>(pipeline);
```  
> [!NOTE]
> All registered pipelines, including those created from `IEventHandler<TEvent>` registrations, can be obtained from the DI container by resolving `PipelineRegistry` (allowing to obtain all pipelines for an event `ImmutableArray<EventPipeline> PipelineRegistry.Get(Type eventType)`). Additionally, `PipelineRegistry.Get(string name)` returns a pipeline by its handler name, and `PipelineRegistry.GetHandlerNames<TEvent>()` returns all handler names registered for an event type.

### Keyed Handlers

 `Add*EventHandler<TEvent, THandler>` and `AddEventHandlerPipeline<TEvent>` support optional parameter `eventBrokerKey`. These handlers are used when event is published by event broker instance with the same key. The optional `handlerName` parameter is used to identify handlers in [persistent event processing](#handler-names).

### Handler Options

`Add*EventHandler<TEvent, THandler>` and `AddEventHandlerPipeline<TEvent>` accept an optional `Action<EventHandlerOptions>` or `Action<PipelineHandlerOptions>` delegate for a more readable, less error-prone configuration:

```csharp
serviceCollection
    .AddTransientEventHandler<TEvent1, THandler1>(o => o
        .ForBroker("broker1")
        .WithHandlerName("my-handler")
        .WithServiceKey("custom-key"))
    .AddScopedEventHandler<TEvent2, THandler2>(o => o
        .ForBroker("broker1"))
    .AddSingletonEventHandler<TEvent3, THandler3>(o => o
        .ForBroker("broker1"));
```

Pipeline handlers use `PipelineHandlerOptions` (which does not have `WithServiceKey`, since pipelines are passed directly and not resolved from DI):

```csharp
serviceCollection.AddEventHandlerPipeline<TEvent>(pipeline, o => o
    .ForBroker("broker1")
    .WithHandlerName("my-pipeline-handler"));
```

## Publishing Events  

Events are published by `IEventBroker.Publish` method.

Events can be published after given time interval with `IEventBroker.PublishDeferred` method.

`IEventBroker.Shutdown()` can be called to stop the event broker from processing events. It signals the cancellation token passed to handlers and stops the internal consumer loop.

> [!WARNING] 
> `PublishDeferred` may not be accurate and may perform badly if large amount of deferred messages are scheduled. It runs a new task that in turn uses `Task.Delay` and then publishes the event.  
A lot of `Task.Delay` means a lot of timers waiting in a queue.

## Logging  

If there is `ILogger` configured in the DI container, `EventBroker` will use it to log when:  
- There is no event handler found for published event (warning). Can be disabled with `DisableMissingHandlerWarningLog()` during configuration.  
- Exception is thrown during event handler resolving (error).
- Exception is thrown from handlers `OnError()` method (error).  
- Exception is thrown from delegate handler (error).  

If there is no logger configured, these exceptions will be handled and swallowed.
  
## Retries  

Retrying within event handler can become a bottleneck. Imagine `EventBroker` is restricted to one concurrent handler. An exception is caught in `Handle` and retry is attempted after given time interval. Since `Handle` is not completed, there is no available "slot" to run other handlers while `Handle` is waiting.  

Another option will be to use `IEventBroker.PublishDeferred`. This will eliminate the bottleneck but will introduce different problems. The same event will be handled again by all handlers, meaning special care should be taken to make all handlers idempotent. If any additional information (e.g. number of retries) needs to be known, it must be carried with the event, introducing accidental complexity.  

To avoid these problems, both `IEventHandler` methods `Handle` and `OnError` have `IRetryPolicy` parameter. It is also available for delegate handlers. 

 `IRetryPolicy.RetryAfter(TimeSpan)` will schedule a retry only for the handler it is called from, without blocking. After the given time interval an instance of the handler or the pipeline will be resolved from the DI container (from a new scope) and executed with the same event instance.

`IRetryPolicy.RetryAfter(Func<uint, TimeSpan, TimeSpan>)` is an overload accepting a function that receives the current attempt number and the last delay, and returns the delay for the next retry. This is useful for implementing patterns like exponential backoff.

`IRetryPolicy.Attempt` is the current retry attempt for a given handler and event.  
`IRetryPolicy.LastDelay` is the time interval before the retry.  

`IRetryPolicy.RetryRequested` is used to coordinate retry request between `Handle` and `OnError`. `IRetryPolicy` is passed to both methods to enable error handling and retry request entirely in `Handle` method. `OnError` can check `IRetryPolicy.RetryRequested` to know whether `Handle` had called `IRetryPolicy.RetryAfter()`.  

`IRetryPolicy.Abandon()` explicitly abandons processing of the event for the handler. `IRetryPolicy.Abandoned` indicates whether `Abandon()` has been called.

If added as a parameter, the `IRetryPolicy` will be passed to the delegate. It has the same behavior, allowing pipelines to be retried too.

> [!NOTE]
> When [persistent events](#persistent-events) are enabled, retries are durable and survive process restarts. The retry state is stored alongside the event record.

> [!WARNING] 
> Retry will not be exactly after the specified time interval in `IRetryPolicy.RetryAfter()`. Take into account a tolerance of around 50 milliseconds. Additionally, retry executions respect maximum concurrent handlers setting, meaning a high load can cause additional delay.

# Persistent Events

EventBrokerSlim supports optional event persistence providing durable, at-least-once event delivery that survives process restarts. Persistence is opt-in - the in-memory broker works without any storage backend. For detailed design rationale, see the [architecture document](EventBrokerSlim-Persistence-Architecture.md) and [ADRs](ADRs/).

## How It Works

When persistence is enabled, `IEventBroker.Publish` writes one record per registered handler to the storage backend and returns - no in-memory dispatch occurs. A background polling loop fetches scheduled records, claims them using optimistic concurrency, and dispatches them to the corresponding handler pipeline. After execution, each record is marked as completed, scheduled for retry, or dead-lettered.

## Configuration

### Event Registry

`EventRegistry` maps event types to stable string names used as identifiers in storage. Each persistent event type must be registered. Register the `EventRegistry` as a singleton in the DI container:

```csharp
var eventRegistry = new EventRegistry()
    .Add<SomeEvent>("SomeEvent")
    .Add<AnotherEvent>("AnotherEvent");

serviceCollection.AddSingleton(eventRegistry);
```

### Handler Names

The `handlerName` parameter on `Add*EventHandler` and `AddEventHandlerPipeline` links a handler to its storage records. Only handlers with a `handlerName` have their names included in fan-out - when an event is published, one storage record is created per registered handler name:

```csharp
serviceCollection
    .AddTransientEventHandler<SomeEvent, SomeEventHandler>(handlerName: "SomeEventHandler")
    .AddEventHandlerPipeline<SomeEvent>(pipeline, handlerName: "SomeEventPipeline");
```

### Publish-Only Handlers

`NullPipeline` enables a publish-only scenario where a process registers handler names for fan-out record creation without processing events locally. Records written by the publishing instance are claimed and processed by other instances that have real handlers registered under the same names.

```csharp
serviceCollection.AddEventHandlerPipeline<SomeEvent>(NullPipeline.Instance, handlerName: "SomeEventHandler");
```

When registered with `NullPipeline.Instance`:
- The handler name is included in fan-out - `Publish` writes a record for this handler to the store
- The handler is excluded from local dispatch - the record is not claimed or processed on this instance
- Another instance with a real handler registered under the same name will claim and process the record

This supports the publisher-only topology: a dedicated process that only publishes events to storage, while separate consumer instances handle processing.

### Storage Backend

Register a storage backend using the builder extensions. For example, with PostgreSQL:

```csharp
serviceCollection.AddEventBroker(x => x
    .WithMaxConcurrentHandlers(3)
    .WithPostgreSqlPersistence((db, settings) =>
    {
        db.ConnectionString = "...";
        db.Schema = "ebs_0";
        settings.PollingInterval = TimeSpan.FromSeconds(10);
        settings.ProcessingTimeout = TimeSpan.FromMinutes(5);
        settings.UnclaimedTtl = TimeSpan.FromDays(7);
        settings.CompletedRecordTtl = TimeSpan.FromDays(7);
        settings.DeadLetteredRecordTtl = TimeSpan.FromDays(30);
    }));
```

`PersistentEventBrokerSettings` options:

| Setting | Default | Description |
|---|---|---|
| `PollingInterval` | 10 seconds | How often the poller checks for scheduled records |
| `ProcessingTimeout` | 5 minutes | In-progress records exceeding this are rescheduled |
| `MaxProcessingTimeouts` | 10 | Max timeouts before a record is dead-lettered |
| `ScheduledBatchSize` | 10 | Number of records fetched per poll |
| `UnclaimedTtl` | 7 days | Unclaimed scheduled records are dead-lettered after this |
| `CompletedRecordTtl` | 7 days | Completed records are deleted after this |
| `DeadLetteredRecordTtl` | 30 days | Dead-lettered records are deleted after this |

### Startup

Call `UsePersistentEventBroker` after building the service provider to start the polling and maintenance loops:

```csharp
var serviceProvider = serviceCollection.BuildServiceProvider();
serviceProvider.UsePersistentEventBroker(throwOnValidationErrors: true);
```

On startup, validation checks that every handler with a `handlerName` has its event type in `EventRegistry`, and every event in `EventRegistry` has at least one named handler (including `NullPipeline` registrations). Set `throwOnValidationErrors: true` for strict mode (default logs warnings).

## Important Considerations

- **At-least-once delivery** - a crash after claiming may cause duplicate processing. Handlers must be idempotent.
- **Escaped exceptions are dead-lettered** - if an exception escapes the pipeline unhandled, the record is immediately dead-lettered. `IRetryPolicy` is not consulted. Handle exceptions inside the pipeline.
- **Name stability** - changing `handlerName` or `EventRegistry` names breaks the link to existing storage records.
- **Serialization** - event types must be serializable. Each `IEventStorage` implementation owns its serialization format.
- **Not event sourcing** - completed records are deleted after `CompletedRecordTtl`.
- **Not a transactional outbox** - writes to storage are not atomic with the caller's database transaction.

