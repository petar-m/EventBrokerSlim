---
layout: default
title: "Getting Started"
nav_order: 2
---

# Getting Started

This guide shows how to register the broker, declare an event and a handler, and publish.

## Install

```
dotnet add package M.EventBrokerSlim
```

## Step 1. Register the broker

`AddEventBroker()` registers the broker. Call it once, regardless of how many events or handlers you add.

```csharp
services.AddEventBroker();
```

It also accepts optional configuration. See [In-memory event broker: Registering the event broker](04-in-memory-broker.md#registering-the-event-broker).

## Step 2. Define an event

An event is any C# type. No interface required. A record works well: it's immutable, so it's safe to hand the same instance to multiple handlers running concurrently.

```csharp
public record ArticlePublished(Guid ArticleId, string Slug);
```

## Step 3. Define a handler

The recommended approach is a **delegate pipeline**. A pipeline is a function (or a chain of functions) that receives the event and any DI services it needs, all injected as parameters. No class to declare, no interface to implement.

```csharp
using M.EventBrokerSlim.DependencyInjection;

IPipeline pipeline = PipelineBuilder.Create()
    .NewPipeline()
    .Execute(async (ArticlePublished e, IEmailService email, CancellationToken ct) =>
    {
        await email.NotifySubscribersAsync(e.ArticleId, ct);
    })
    .Build()
    .Pipelines[0];
```

`Build()` produces the pipeline; `Pipelines[0]` takes the single one defined here.

The delegate receives the published `ArticlePublished` instance directly. Any service registered in DI can be added as a parameter alongside it.

Pipelines can also chain multiple delegates to layer cross-cutting concerns. See the [Pipelines guide](03-pipelines/).

## Step 4. Register the handler

Register the pipeline for its event type. Do this once per event type you handle:

```csharp
services.AddEventHandlerPipeline<ArticlePublished>(pipeline);
```

`AddEventHandlerPipeline()` links the pipeline to `ArticlePublished` events.

## Step 5. Publish

Inject `IEventBroker` wherever you publish events:

```csharp
public class ArticleService
{
    private readonly IEventBroker _broker;

    public ArticleService(IEventBroker broker) => _broker = broker;

    public async Task PublishArticleAsync(Article article, CancellationToken ct)
    {
        // ... your publishing logic ...

        await _broker.Publish(new ArticlePublished(article.Id, article.Slug), ct);
    }
}
```

`Publish()` writes the event to an in-memory channel and returns immediately.

## What happens next

When `Publish()` is called:

1. The event is written to an internal `Channel<object>`.
2. A single consumer loop picks it up and resolves all registered handlers for that event type.
3. Each handler runs on the ThreadPool in its own DI scope. The scope is disposed when the handler completes.

## Alternative: class-based handlers

If you prefer a class, implement `IEventHandler<TEvent>`:

```csharp
public class ArticlePublishedHandler : IEventHandler<ArticlePublished>
{
    private readonly IEmailService _email;

    public ArticlePublishedHandler(IEmailService email) => _email = email;

    public async Task Handle(ArticlePublished e, IRetryPolicy retry, CancellationToken ct)
    {
        await _email.NotifySubscribersAsync(e.ArticleId, ct);
    }

    public Task OnError(Exception ex, ArticlePublished e, IRetryPolicy retry, CancellationToken ct)
    {
        // Called when Handle throws. Use retry.RetryAfter(...) to retry.
        return Task.CompletedTask;
    }
}
```

Register it:

```csharp
services.AddTransientEventHandler<ArticlePublished, ArticlePublishedHandler>();
```

Class-based and pipeline handlers can coexist. See [In-memory broker](04-in-memory-broker/) for mixing handler styles and registering multiple handlers per event.

## Next steps

- [In-memory broker](04-in-memory-broker/). Retries without blocking, dynamic handlers, and multiple broker instances.
- [Persistent Events](05-persistent-events/). Add durability with a single configuration change.

