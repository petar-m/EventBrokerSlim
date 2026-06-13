---
layout: default
title: "Pipelines"
nav_order: 3
---

# Pipelines

A pipeline is a chain of functions composed by nesting. Each function runs in order and decides whether to call the next one. Every step wraps the rest of the chain, so it can run code before and after everything downstream.

Pipelines are the recommended way to define handlers in EventBrokerSlim. They compose small functions instead of classes. Each step is independently testable, and cross-cutting concerns (logging, validation, timing, error handling) layer around the business logic without touching it.

Pipelines come from [FuncPipeline](https://www.nuget.org/packages/FuncPipeline), a standalone library. EventBrokerSlim depends on it but does not wrap it: the `PipelineBuilder`, `IPipeline`, and `PipelineRunContext` types on this page are FuncPipeline's. FuncPipeline is usable on its own, with no event broker involved, anywhere a composable function chain is useful. See [Using FuncPipeline on its own](#using-funcpipeline-on-its-own).

## How a pipeline runs

Each function receives the parameters it asks for plus an `INext` handle. Calling `INext.RunAsync()` runs the rest of the pipeline. Code before that call runs on the way in. Code after it runs on the way out, after every later function has finished.

```csharp
PipelineBuilder.Create()
    .NewPipeline()
    .Execute(async (INext next) =>
    {
        Console.WriteLine("Before A");
        await next.RunAsync();
        Console.WriteLine("After A");
    })
    .Execute(async () => Console.WriteLine("A"))
    .Build();

// Output:
// Before A
// A
// After A
```

A function that does not call `next` ends the chain. Everything after it is skipped. This is what makes the model composable:

- Wrap `next.RunAsync()` in a `try/catch` and the function becomes an error handler for everything downstream.
- Wrap it in a stopwatch and the function becomes a timer.
- Skip the call conditionally and the function becomes a gate.

The business step usually sits last and simply does its work without calling `next`.

## Why pipelines over classes

- No boilerplate. No class declaration, no interface to satisfy, no constructor.
- Each step is a standalone function, independently testable without the broker.
- Middleware (logging, error handling, correlation, timing) composes naturally via `INext`.
- `IRetryPolicy`, `CancellationToken`, and any registered service are parameters each step declares only when it needs them, in any order, instead of a fixed method signature.

Class-based handlers remain available when a class structure helps. See [In-memory broker](04-in-memory-broker.md). Internally they are converted to pipelines, so the two are the same mechanism.

## Building a pipeline

Pipelines are built with the fluent `PipelineBuilder` API. `NewPipeline()` starts one, each `Execute()` adds a function, and `Build()` finalizes it.

```csharp
PipelineBuilder builder = PipelineBuilder.Create()
    .NewPipeline()
    .Execute(async (ILogger<Program> logger, INext next) =>
    {
        try
        {
            await next.RunAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Article handler failed");
        }
    })
    .Execute(async (ArticlePublished e, ISearchIndex index, CancellationToken ct) =>
    {
        await index.UpdateAsync(e.ArticleId, ct);
    })
    .Build();

IPipeline pipeline = builder.Pipelines[0];
```

`Build()` returns the builder, not the pipeline. The finished pipelines are exposed on `PipelineBuilder.Pipelines`, in definition order, so a single-pipeline build ends in `.Pipelines[0]`. To build several from one builder, call `NewPipeline()` again after `Build()`; each adds another entry to `Pipelines`. `Build(Action<IPipeline>? onBuild)` takes an optional callback invoked with the pipeline just built, useful for registering it as you go.

`Execute()` has overloads for zero through sixteen typed parameters. Parameters can appear in any order, and only the ones a function declares are resolved for it.

## Delegate parameters

When a pipeline runs as an EventBrokerSlim handler, the event broker makes these available as parameters in any order:

| Parameter              | Source                                   |
| ---------------------- | ---------------------------------------- |
| Your event             | The published event instance             |
| `IRetryPolicy`         | Retry control for this handler execution |
| `CancellationToken`    | The broker's cancellation token          |
| `INext`                | Calls the next function in the pipeline  |
| `PipelineRunContext`   | The run's shared data bag (see below)    |
| Any DI-registered type | Resolved from the per-execution scope    |

The event instance and `IRetryPolicy` are not magic: the event broker places them into the run's `PipelineRunContext` before executing the pipeline, which is why they resolve as parameters like anything else. `INext`, `PipelineRunContext`, and `CancellationToken` are always available in any FuncPipeline run, broker or not.

## Parameter resolution

For each parameter, FuncPipeline resolves a value in this order:

1. From the `IServiceProvider` (a DI scope).
2. From the `PipelineRunContext`.
3. The type's default value, if neither source has it.

Dependencies are always resolved from a service scope, never the root provider. In EventBrokerSlim you do not supply that scope: the event broker assigns its own `IServiceScopeFactory` to every pipeline it runs (one scope per function by default; see [Service scopes per function](#service-scopes-per-function)). In standalone use you provide one (see below).

`PipelineRunContext` is a type-keyed bag (`Dictionary<Type, object>` underneath). Use it to pass values that are not registered services, and to carry data between functions:

```csharp
.Execute(async (PipelineRunContext context, ArticlePublished e, INext next) =>
{
    context.Set<ArticleMetrics>(new ArticleMetrics(e.ArticleId));
    await next.RunAsync();
})
.Execute((ArticleMetrics metrics, IAnalytics analytics, CancellationToken ct) =>
    analytics.RecordAsync(metrics, ct))
```

Anything `Set` into the context resolves by its type in later functions, as if it were a registered service. Its API is `Set<T>` / `TryGet<T>` (plus `Set(Type, object)`, `Remove(Type)`, and `Clear`).

## Controlling resolution: `[ResolveFrom]`

By default a parameter is tried in the service provider first, then the context, then defaulted. The `[ResolveFrom]` attribute overrides that per parameter:

```csharp
.Execute(async (
    [ResolveFrom(PrimarySource = Source.Context, Fallback = false, PrimaryNotFound = NotFoundBehavior.ThrowException)]
    ArticleMetrics metrics,
    [ResolveFrom(Key = "editorial")]
    IEmailService email) =>
{
    // metrics must be in the context or the run fails
    // email is resolved as a keyed service
})
```

| Property            | Effect                                                                                  | Default             |
| ------------------- | --------------------------------------------------------------------------------------- | ------------------- |
| `PrimarySource`     | Where to look first: `Source.Services` or `Source.Context`                              | `Services`          |
| `Fallback`          | Whether to try the other source if the primary misses                                   | `true`              |
| `PrimaryNotFound`   | If the primary misses and there is no fallback: `ThrowException` or `ReturnTypeDefault` | `ReturnTypeDefault` |
| `SecondaryNotFound` | Same choice for a missed fallback source                                                | `ReturnTypeDefault` |
| `Key`               | Service key, for keyed DI registrations                                                 | none                |

Setting `PrimaryNotFound = ThrowException` turns a missing dependency into a hard failure instead of a silently defaulted parameter. The attribute can decorate the parameter directly, or be supplied when calling `Execute` via a `Dictionary<int, ResolveFromAttribute>` keyed by parameter position. The two forms are equivalent.

## Service scopes per function

By default each function in a run gets its own DI scope, created just before it executes and disposed just after. Scoped and transient services are therefore fresh per function, not shared down the chain. Pass `PipelineRunOptions` to `NewPipeline` to share one scope across the whole run instead:

```csharp
PipelineBuilder.Create()
    .NewPipeline(new PipelineRunOptions { ServiceScopePerFunction = false })
    .Execute(/* ... */)
    .Build();
```

Share a scope when functions must see the same scoped instances (for example a `DbContext` or a unit-of-work spanning the chain). Keep the default when each step should be isolated. Singletons are shared either way.

## Multiple pipelines per event

Register multiple pipelines for the same event type. Each runs independently:

```csharp
services
    .AddEventBroker()
    .AddEventHandlerPipeline<ArticlePublished>(emailPipeline)
    .AddEventHandlerPipeline<ArticlePublished>(auditPipeline);
```

## Using FuncPipeline on its own

FuncPipeline has no dependency on EventBrokerSlim. Add the package and you can build and run pipelines anywhere: request handling, batch steps, or any place nested function composition fits.

```csharp
dotnet add package FuncPipeline
```

Outside the broker you run the pipeline yourself and supply the inputs the event broker would otherwise provide. Pass an `IServiceScopeFactory` to resolve services, and a `PipelineRunContext` to seed values and read results back:

```csharp
// A scope factory, from your application's IServiceProvider, if functions resolve services from DI.
IServiceScopeFactory scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

IPipeline pipeline = PipelineBuilder.Create(scopeFactory)
    .NewPipeline()
    .Execute(async (Article article, ISlugGenerator slugs, PipelineRunContext context) =>
    {
        context.Set<string>(await slugs.CreateAsync(article.Title));
    })
    .Build()
    .Pipelines[0];

// Seed the inputs the function needs, run, then read the output back.
var context = new PipelineRunContext().Set<Article>(article);
PipelineRunResult result = await pipeline.RunAsync(context);

if (result.IsSuccessful && result.Context.TryGet<string>(out var slug))
{
    Console.WriteLine(slug);
}
```

`RunAsync` accepts an optional `PipelineRunContext` and `CancellationToken`, and never throws. It reports outcome through `PipelineRunResult`: `IsSuccessful`, the captured `Exception` if a function threw, and `Context` for reading values left behind by the run. EventBrokerSlim builds its own error handling (`OnError`, retry policies, dead-lettering) on top of this result. That behavior is covered in [In-memory broker](04-in-memory-broker.md).

If you do not need DI, call `PipelineBuilder.Create()` with no scope factory and rely on the context for inputs.

## Next steps

- [In-memory broker](04-in-memory-broker.md). Retries without blocking, dynamic handlers, and multiple broker instances.
- [Persistent Events](05-persistent-events.md). Add durability with a single configuration change.

