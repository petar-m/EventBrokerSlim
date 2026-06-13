---
title: In-memory event broker
nav_order: 4
---

# In-memory event broker

This page covers the in-memory event broker in depth: how it dispatches events, the handler lifecycle, and the full feature surface.

Delegate pipelines, the recommended handler model, have their own page: [Pipelines](03-pipelines.md). For durability across restarts and multiple instances, see [Persistent Events](05-persistent-events.md).

## How the in-memory event broker works

The event broker decouples publishers from consumers with a `System.Threading.Channels.Channel<object>`. Publishing writes the event to the channel and returns immediately. There is no backpressure and no blocking.

A single background loop drains the channel. For each event it resolves every handler registered for that event type and schedules them on the ThreadPool, where a configurable semaphore caps how many run concurrently.

Both handler models run as pipelines: a class-based handler is compiled into one, with its `Handle()` and `OnError()` wrapped as pipeline functions. A delegate pipeline and a class-based handler execute through the same machinery.

## Registering the event broker

Register a broker with `AddEventBroker()`. The optional configuration callback sets broker-wide behavior:

```csharp
services.AddEventBroker(x => x
    .WithMaxConcurrentHandlers(4)
    .DisableMissingHandlerWarningLog());
```

- **`WithMaxConcurrentHandlers(n)`** sets the concurrency limit enforced by the semaphore. Default is 2.
- **`DisableMissingHandlerWarningLog()`** silences the warning logged when an event is published with no registered handler. The warning is on by default.

### Multiple event broker instances

Use `AddKeyedEventBroker()` to register independent broker instances in the same process. Each needs a unique key, and only one default (unkeyed) broker is allowed. Handlers bind to a broker through the `eventBrokerKey` argument and must use the same key as their broker.

```csharp
services
    .AddKeyedEventBroker("editorial")
    .AddEventHandlerPipeline<ArticlePublished>(articlePipeline, eventBrokerKey: "editorial");

services
    .AddKeyedEventBroker("notifications")
    .AddEventHandlerPipeline<Notification>(notifyPipeline, eventBrokerKey: "notifications");
```

Resolve a keyed broker via `IServiceProvider.GetRequiredKeyedService<IEventBroker>("editorial")`.

Each keyed broker has its own concurrency setting, handler set, and lifecycle, and they share no state. When persistence is enabled, all broker instances share a single global `EventRegistry`. See [Persistent Events](05-persistent-events.md#event-registry).

## Registering handlers

Register handlers on the service collection. Without a key, they attach to the default broker.

```csharp
services
    .AddEventHandlerPipeline<ArticlePublished>(notifyPipeline)
    .AddTransientEventHandler<ArticlePublished, ArticlePublishedHandler>();
```

`AddEventHandlerPipeline()` registers a delegate pipeline. `AddTransientEventHandler()`, `AddScopedEventHandler()`, and `AddSingletonEventHandler()` register a class-based handler with the matching DI lifetime, which controls instance reuse the same way it does for any registered service. Any number of pipelines and handlers can target the same event type, and all of them run on publish. The optional `handlerName` argument is used only by persistent events. See [Persistent Events](05-persistent-events.md#handler-names).

Each registration method also has an overload taking a configuration delegate instead of positional arguments. The fluent form reads better when you set more than one option:

```csharp
services
    .AddEventHandlerPipeline<ArticlePublished>(notifyPipeline, o => o
        .WithHandlerName("article-published-email"))
    .AddTransientEventHandler<ArticlePublished, ArticlePublishedHandler>(o => o
        .WithHandlerName("article-published-audit")
        .WithServiceKey("audit"));
```

- **`ForBroker(key)`** binds the handler to a keyed broker (the delegate equivalent of the `eventBrokerKey` argument).
- **`WithHandlerName(name)`** sets the persistent handler name (equivalent to `handlerName`).
- **`WithServiceKey(key)`** sets a custom DI service key for the handler registration. Class-based handlers only.

## Publishing an event

Publish an event with `Publish()`. It returns without waiting for any handler.

```csharp
await broker.Publish(new ArticlePublished(Guid.NewGuid(), "my-first-article"));
```

`Publish()` takes an optional `CancellationToken` that cancels the publish call itself, not the handlers it triggers. After `Shutdown()`, any further publish throws `EventBrokerPublishNotAvailableException`.

### Deferred publishing

`PublishDeferred()` schedules the event after a delay.

```csharp
await broker.PublishDeferred(new ArticlePublished(id, slug), TimeSpan.FromMinutes(30));
```

There is no cancellation token parameter; cancellation is handled by the event broker's shutdown token. It uses `Task.Delay()` internally, so each call adds a timer. Do not use it for a large number of deferred events. Deferred events do not survive process restart.

With persistence enabled, deferred publishing is durable and survives restart. See [Persistent Events](05-persistent-events.md#deferred-publishing).

## Handler lifecycle and DI

Handler executions run in DI scopes the event broker creates, not an ambient request scope. A pipeline, by default, creates a fresh scope for each delegate in the chain, and can be configured to share one scope across the whole run. A class-based handler runs in a single scope per execution. See [Pipelines: Service scopes per function](03-pipelines.md#service-scopes-per-function).

Within a scope:

- **Transient and scoped services** are fresh.
- **Singleton services** are shared across all executions (same instance for the lifetime of the container).

Pipeline delegate parameters are resolved from the active scope just before each delegate runs. Constructor dependencies of a class-based handler are resolved when its scope is created.

If handler resolution fails (e.g., a required service is missing), the exception is caught and logged.

## Class-based handlers

Implement `IEventHandler<TEvent>` when a class structure helps. For example, when constructor injection of many services is cleaner than a long delegate parameter list. Or when you need shared state across `Handle()` and `OnError()`.

```csharp
public class ArticlePublishedHandler : IEventHandler<ArticlePublished>
{
    private readonly IEmailService _email;

    public ArticlePublishedHandler(IEmailService email) => _email = email;

    public async Task Handle(ArticlePublished e, IRetryPolicy retry, CancellationToken ct)
    {
        await _email.NotifySubscribersAsync(e.ArticleId, ct);
    }

    public async Task OnError(Exception ex, ArticlePublished e, IRetryPolicy retry, CancellationToken ct)
    {
        if (retry.Attempt < 3)
            retry.RetryAfter(TimeSpan.FromSeconds(Math.Pow(2, retry.Attempt)));
        else
            retry.Abandon();
    }
}
```

`Handle()` is the main processing method. `OnError()` runs when `Handle()` throws. Both receive an `IRetryPolicy` instance, covered in the next section.

## Exception handling and retries

Publishing is fire-and-forget, so a handler runs with no caller waiting on it. An exception it throws has nowhere to propagate: it cannot reach the code that published the event. Each handler deals with its own failures.

A delegate pipeline handles errors as a step in the chain. A function placed early wraps the call to the rest of the pipeline in a try/catch, inspects the failure, and decides whether to request a retry. An exception that no step handles is captured by the pipeline rather than thrown, and the event broker logs it.

A class-based handler gets an explicit hook instead. When `Handle()` throws, the event broker calls `OnError()` on the same instance, passing the exception. If an exception still escapes, for example `OnError()` itself throws, it is swallowed and logged when an `ILogger` is configured. Either way the dispatch loop keeps running.

On failure you have two options: retry the event, or give up. Both are driven by the `IRetryPolicy` the handler receives. A class-based handler gets it as a parameter on `Handle()` and `OnError()`; a delegate pipeline takes it as a delegate parameter. You can request a retry from any of them, including from inside `Handle()` itself.

Request a retry with `IRetryPolicy.RetryAfter(delay)`. It records the request and returns, so the handler finishes and frees its concurrency slot right away. After the delay a new scope is created and the handler runs again with the same event.

Do not retry by sleeping. A handler that loops on `await Task.Delay(...)` holds its concurrency slot for the whole wait, so with a low concurrency limit a few sleeping handlers can stall every other event. `RetryAfter()` avoids this: the wait happens off the slot, not inside the handler.

A retry does not require an exception. A handler that completes normally can still call `RetryAfter()` to have the same event run again later, for instance to wait for a condition before processing.

The delay is either a fixed `TimeSpan` or a function of the attempt number and the previous delay:

```csharp
var pipeline = PipelineBuilder.Create()
    .NewPipeline()
    .Execute(async (INext next, IRetryPolicy retry) =>
    {
        try
        {
            await next.RunAsync();
        }
        catch (TransientException)
        {
            if (retry.Attempt < 3)
                retry.RetryAfter((attempt, lastDelay) => TimeSpan.FromSeconds(Math.Pow(2, attempt)));
            else
                retry.Abandon();
        }
    })
    .Execute(async (ArticlePublished e, IEmailService email, CancellationToken ct) =>
    {
        await email.NotifySubscribersAsync(e.ArticleId, ct);
    })
    .Build()
    .Pipelines[0];
```

### Retry state

| Member                        | What it tells you                                   |
| ----------------------------- | --------------------------------------------------- |
| `IRetryPolicy.Attempt`        | Current attempt number (1 on the first retry)       |
| `IRetryPolicy.LastDelay`      | The delay used before the current attempt           |
| `IRetryPolicy.RetryRequested` | Whether `RetryAfter()` was called in this execution |
| `IRetryPolicy.Abandoned`      | Whether `Abandon()` was called                      |

The same `IRetryPolicy` instance is shared throughout one handler's processing of an event. Every delegate in a pipeline can take it as a parameter, and a class-based handler's `Handle()` and `OnError()` both receive it. It also persists across that handler's retries, which is how `Attempt` and `LastDelay` advance. Each handler has its own instance; the policy is never shared between handlers.

That shared instance is what lets the parts coordinate. `RetryRequested` tells a later delegate, or `OnError()`, that a retry was already asked for, so it does not add a second one. `Abandoned` records that the handler gave up. `OnError()` runs only when `Handle()` throws: if `Handle()` requested a retry before the exception escaped, `OnError()` sees `RetryRequested` and leaves it; otherwise it can call `Abandon()`.

`IRetryPolicy.Abandon()` marks the event as abandoned and cancels any retry, including one requested earlier in the same execution. The in-memory broker has no dead-letter store, so an abandoned event is simply dropped. That is the same outcome as not requesting a retry, so calling `Abandon()` is optional here. It is still worth using to state "give up" explicitly, and it matters once persistence is enabled: an abandoned record is moved to dead-letter instead of completed. See [Persistent Events](05-persistent-events.md).

> Retry timing is approximate. The actual delay can be longer than requested, especially under load when no concurrency slot is free. With persistent events it also depends on the configured polling interval.

## Dynamic handlers

> Dynamic handlers are an in-memory feature. They are not available with persistent events.

Delegate pipelines can be added and removed after the DI container is built; class-based handlers cannot.

```csharp
public class ModerationGate : IDisposable
{
    private readonly IDynamicEventHandlers _dynamicHandlers;
    private IDynamicHandlerClaimTicket? _ticket;

    public ModerationGate(IDynamicEventHandlers dynamicHandlers) => _dynamicHandlers = dynamicHandlers;

    public void Enable()
    {
        var pipeline = PipelineBuilder.Create()
            .NewPipeline()
            .Execute(Review)
            .Build()
            .Pipelines[0];

        _ticket = _dynamicHandlers.Add<ArticlePublished>(pipeline);
    }

    private Task Review(ArticlePublished e, IModerationService moderator, CancellationToken ct)
        => moderator.ReviewAsync(e, ct);

    public void Disable()
    {
        if (_ticket is not null)
        {
            _dynamicHandlers.Remove(_ticket);
            _ticket = null;
        }
    }

    public void Dispose() => Disable();
}
```

`IDynamicEventHandlers` is registered automatically by `AddEventBroker()`. Always keep the `IDynamicHandlerClaimTicket`. Without it, you cannot remove the handler and will leak it for the lifetime of the container.
