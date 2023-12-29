# EventBrokerSlim  

An implementation of broadcasting events in a fire-and-forget style.  

This was supposed to be next vesion of [EventBroker](https://github.com/petar-m/EventBroker), however it diverged so much than it became its own package.  
It is trimmed down to minimum public surface and essential functionality.

It still is:
- in-memory, in-process
- publishing is *Fire and Forget* style  
- events don't need to implement specific interface  
- event handlers are runned on background threads  

And also:
- events are handled on a ThreadPool threads
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

    public async Task Handle(SomeEvent @event)
    {
        // process the event
    }

    public Task OnError(Exception exception, SomeEvent @event)
    {
        // called on unhandled exeption from Handle 
    }
}
```

Add event broker impelementation to DI container using `AddEventBroker` extension method and register handlers:

```csharp
serviceCollection.AddEventBroker(
     x => x.AddKeyedTransient<SomeEvent, SomeEventHandler>());
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

EventBroker uses System.Threading.Channel.  

There are no limits for publishers. Publishing is as fast as writing to a channel.  

Handlers are resolved by the event type in a new scope and disposed after handler comletes. Each handler execution is scheduled on the ThreadPool. No more than configured maximum handlers run concurrently.
  
![](docs/event_broker.png)

# Details

## Events

Events can be of any type. A best pracice for event is to be immutable, since it can be processed by multiple handlers in different threads.  

## Event Handlers

Event handlers need to implement `IEventHandler<TEvent>` interface.  
Each event handler is executed on a background thread. Each event handler is resovled from its own DI container scope.

## Configuration  

EventBroker is depending on `Microsoft.Extentions.DependencyInjection` container for resolving event handlers.  
EventBroker guarantees that each handler is resolved in its own scope which is disposed after the handler completes.  

EventBroker is configured using the `AddEventBroker` extension method of `IServiceCollection`.  
Event handlers and event broker behavior are configured using the confiuration delegate.  
Event handlers are registered by the event type and a corresponding `IEventHandler` implementation as transient, scoped, or singleton.  

*Example:*
```csharp
services.AddEventBroker(
    x => x.AddKeyedTransient<Event1, EventHandler1>()
          .AddKeyedScoped<Event2, EventHandler2>()
          .AddKeyedSingleton<Event3, EventHandler3>()
          .WithMaxConcurrentHandlers(3)
          .DisableMissingHandlerWarningLog())
```  
There can be multiple handlers for the same event.  

The `AddKeyed*` naming may be confusing since no key is provided. This comes from the need to create a scope and resolve the handler from this scope. Since there can be multiple implementations of the same interface, `GetService` or `GetServices` will get either the last one registered or all of them. This is solved by internally generating a key for each registration. Then exactly one keyed service (event handler) is resolved per scope.  

Event handlers can be configured separately by providing a configuration action but still have to be passed to `AddEventBroker` method.  
*Example:*
```csharp
public class SomeHandlers
{
    public static Action<EventHandlerRegistryBuilder> Register()
    {
        return x => x.AddKeyedTransient<Event1, Handler1>()
                     .AddKeyedScoped<Event2, Handler2>();
    }
}

public class OtherHandlers
{
    public static Action<EventHandlerRegistryBuilder> Register()
    {
        return x => x.AddKeyedScoped<Event1, Handler3>();
    }
}

services.AddEventBroker(
    x => x.Add(SomeHandlers.Register())
          .Add(OtherHandlers.Register()))
```  

Note that handlers registered outside of `AddEventBroker` method will be ignored.

`WithMaxConcurrentHandlers` defines how many handlers can run at the same time. Default is 2.  

`DisableMissingHandlerWarningLog` suppresses logging warnins when there is no handler found for event.  

## Publishing Events  

Events are published using `IEventBroker.Publish` method.

`IEventBroker` and its implementation are registered in the DI container by the `AddEventBroker` method.

## Exception Handling  

Since event handlers are executed on background threads, there can be no unhandled ecxeptions.  

An exception thrown from `Handle` method is caught and passed to `OnError` method of the same handler instance (may be on another thread however).  

An exception thrown from `OnError` is handled and swallowed and potentially logged.  

## Logging  

If there is logging configured in the DI container, EventBroker will use it to log when:  
- There is no event handler found for published event (warning). Can be disabled with `DisableMissingHandlerWarningLog()` during configuration.  
- Exception is thrown during event handler resolving (error).
- Exception is thrown from handlers `OnError()` method (error).  

If there is no logger configured, these exceptions will be handled and swallowed.