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
