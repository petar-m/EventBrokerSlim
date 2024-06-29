# EventBrokerSlim  

An implementation of broadcasting events in a fire-and-forget style.  

Features:  
- in-memory, in-process
- publishing is *Fire and Forget* style  
- events don't have to implement specific interface  
- event handlers are executed on a `ThreadPool` threads  
- the number of concurrent handlers running can be limited  
- built-in retry option
- tightly integrated with Microsoft.Extensions.DependencyInjection
- each handler is resolved and executed in a new DI container scope
- **NEW** event handlers can be delegates     

# How does it work

Implement an event handler by implementing `IEventHandler<TEvent>` interface:

```csharp
public record SomeEvent(string Message);

public class SomeEventHandler : IEventHandler<SomeEvent>
{
    // Inject services added to the DI container
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

or use `DelegateHandlerRegistryBuilder` to register delegate as handler: 

```csharp
DelegateHandlerRegistryBuilder builder = new();
builder.RegisterHandler<SomeEvent>(
    static async (SomeEvent someEvent, ISomeService service, CancellationToken cancellationToken) =>
    {
        await service.DoSomething(someEvent, cancellationToken);
    });
```  

Add event broker implementation to DI container using `AddEventBroker` extension method and register handlers, optionally add delegate handler registries:

```csharp
serviceCollection.AddEventBroker(x => x.AddTransient<SomeEvent, SomeEventHandler>())
                 .AddSingleton(builder);
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
