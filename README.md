# EventBrokerSlim  
  

[![build](https://github.com/petar-m/EventBrokerSlim/actions/workflows/build.yml/badge.svg)](https://github.com/petar-m/EventBrokerSlim/actions)
[![NuGet](https://img.shields.io/nuget/v/M.EventBrokerSlim.svg)](https://www.nuget.org/packages/M.EventBrokerSlim)    

An implementation of broadcasting events in a fire-and-forget style.  

Features:  
- in-memory, in-process
- publishing is *Fire and Forget* style  
- events don't have to implement specific interface  
- event handlers are runned on a `ThreadPool` threads  
- the number of concurrent handlers running can be limited  
- built-in retry option
- tightly integrated with Microsoft.Extensions.DependencyInjection
- each handler is resolved and runned in a new DI container scope

# How does it work

Implement an event handler by implementing `IEventHadler<TEvent>` interface:

```csharp
public record SomeEvent(string Message);

public class SomeEventHandler : IEventHandler<SomeEvent>
{
    // Inject services added to the DI container
    public SomeEventHandler()
    {
    }

    public async Task Handle(SomeEvent @event, RetryPolicy retryPolicy, CancellationToken cancellationToken)
    {
        // process the event
    }

    public async Task OnError(Exception exception, SomeEvent @event, RetryPolicy retryPolicy, CancellationToken cancellationToken)
    {
        // called on unhandled exeption from Handle 
        // optionally use retryPolicy.RetryAfter(TimeSpan)
    }
}
```

Add event broker impelementation to DI container using `AddEventBroker` extension method and register handlers:

```csharp
serviceCollection.AddEventBroker(
     x => x.AddTransient<SomeEvent, SomeEventHandler>());
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

# Design  

`EventBroker` uses `System.Threading.Channels.Channel<T>` to decouple procucers from consumers.  

There are no limits for publishers. Publishing is as fast as writing an event to a channel.  

Event handlers are resolved by event type in a new DI scope which is disposed after handler comletes. Each handler execution is scheduled on the `ThreadPool` without blocking the producer. No more than configured maximum handlers run concurrently.
  
![](docs/event_broker.png)

# Details

## Events

Events can be of any type. A best pracice for event is to be immutable - may be processed by multiple handlers in different threads.  

## Event Handlers

Event handlers have to implement `IEventHandler<TEvent>` interface and to be registered in the DI container.  
For each event handler a new DI container scope is created and the event handler is resolved from it. This way it can safely use injected services.  
Every event handler is scheduled for execution on the `ThreadPool` without blocking the producer.

## Configuration  

`EventBroker` is depending on `Microsoft.Extentions.DependencyInjection` container for resolving event handlers.  
It guarantees that each handler is resolved in a new scope which is disposed after the handler completes.  

`EventBroker` is configured with `AddEventBroker` and `AddEventHandlers` extension methods of `IServiceCollection` using a confiuration delegate.  
Event handlers are registered by the event type and a corresponding `IEventHandler` implementation as transient, scoped, or singleton.  

*Example:*
```csharp
services.AddEventBroker(
    x => x.WithMaxConcurrentHandlers(3)
          .DisableMissingHandlerWarningLog()
          .AddTransient<Event1, EventHandler1>()
          .AddScoped<Event2, EventHandler2>()
          .AddSingleton<Event3, EventHandler3>())
```  

`WithMaxConcurrentHandlers` defines how many handlers can run at the same time. Default is 2.  

`DisableMissingHandlerWarningLog` suppresses logging warning when there is no handler found for event.  

`EventBroker` behavior and event handlers can be configured with separate extension methods. The order of calls to `AddEventBroker` and `AddEventHandlers` does not matter. 

*Example:*
```csharp
services.AddEventBroker(
            x => x.WithMaxConcurrentHandlers(3)
                  .DisableMissingHandlerWarningLog());

services.AddEventHandlers(
            x => x.AddTransient<Event1, EventHandler1>()
                  .AddScoped<Event2, EventHandler2>()
                  .AddSingleton<Event3, EventHandler3>())
```  

There can be multiple handlers for the same event.  

Note that handlers **not** registered using `AddEventBroker` or `AddEventHandlers` methods will be **ignored** by `EventBroker`.  

## Publishing Events  

`IEventBroker` and its implementation are registered in the DI container by the `AddEventBroker` method.

Events are published using `IEventBroker.Publish` method.

Events can be published after given time interval with `IEventBroker.PublishDeferred` method.

**Caution**: `PublishDeferred` may not be accurate and may perform badly if large amount of deferred messages are scheduled. It runs a new task that in turn uses `Task.Delay` and then publishes the event.  
A lot of `Task.Delay` means a lot of timers waiting in a queue.

## Exception Handling  

Since event handlers are executed on the `ThreadPool`, there is nowhere to propagate unhandled ecxeptions.  

An exception thrown from `Handle` method is caught and passed to `OnError` method of the same handler instance (may be on another thread however).  

An exception thrown from `OnError` is handled and swallowed and potentially logged.  

## Logging  

If there is logging configured in the DI container, `EventBroker` will use it to log when:  
- There is no event handler found for published event (warning). Can be disabled with `DisableMissingHandlerWarningLog()` during configuration.  
- Exception is thrown during event handler resolving (error).
- Exception is thrown from handlers `OnError()` method (error).  

If there is no logger configured, these exceptions will be handled and swallowed.
  
## Retries  

Retrying within event hadler can become a bottleneck. Imagine `EventBroker` is restricted to one concurrent handler. An exception is caught in `Handle` and retry is attempted after given time interval. Since `Handle` is not completed, there is no available "slot" to run other handlers while `Handle` is waiting.  

Another option will be to use `IEventBroker.PublishDeferred`. This will eliminate the bottleneck but will itroduce different problems. The same event will be handled again by all handlers, meaning specaial care should be taken to make all handlers idempotent. Any additional information (e.g. number of retries) needs to be known, it should be carried with the event, introducing accidential complexity.  

To avoid these problems, both `IEventBroker` `Handle` and `OnError` methods have `RetryPolicy` parameter.  

 `RetryPolicy.RetryAfter()` will schedule a retry only for the handler it is called from, without blocking. After the given time interval an instance of the handler will be resolved from the DI container (from a new scope) and executed with the same event instance.

`RetryPolicy.Attempt` is the current retry attempt for a given handler and event.  
`RetryPolicy.LastDelay` is the time interval before the retry.  

`RetryPolicy.RetryRequested` is used to coordinate retry request between `Handle` and `OnError`. `RetryPolicy` is passed to both methods to enable error handling and retry request entirely in `Handle` method. `OnError` can check `RetryPolicy.RetryRequested` to know whether `Hanlde` had called `RetryPolicy.RetryAfter()`.  

**Caution:** the retry will not be exactly after the specified time interval in `RetryPolicy.RetryAfter()`. Take into account a tolerance of around 50 milliseconds. Additionally, retry executions respect maximum concurrent handlers setting, meaning a high load can cause additional delay.

