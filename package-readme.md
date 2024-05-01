# EventBrokerSlim  

An implementation of broadcasting events in a fire-and-forget style.  

Features:  
- in-memory, in-process
- publishing is *Fire and Forget* style  
- events don't have to implement specific interface  
- event handlers are runned on a `ThreadPool` threads  
- the number of concurrent handlers running can be limited  
- built-in retry option
- tightly integrated with Microsoft.Extensions.DependencyInjection
- each handler is resolved and runned in a new DI container scopee

# How does it work

Define an event handler by implementing `IEventHadler<TEvent>` interface:

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


Use `AddEventBroker` extension method to register `IEventBroker` and handlers:

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

