# Persistent Events: Redis

Redis is an in-memory data store with optional persistence. This backend stores events in Redis data structures, shared across application instances. See [Choosing a backend](05-persistent-events.md#choosing-a-backend) to compare the alternatives.

## Install

```
dotnet add package M.EventBrokerSlim.PersistentEvents.Redis
```

## Schema setup

None required. Redis data structures are created on first use.

## Configuration

### With a connection string

```csharp
var registry = new EventRegistry()
    .Add<ArticlePublished>("article-published");

services
    .AddSingleton(registry)
    .AddEventBroker(x => x
        .WithRedisPersistence((redis, settings) =>
        {
            redis.ConnectionString = "localhost:6379";
            redis.KeyPrefix = "ebs_0"; // optional, default: "ebs_0"

            settings.PollingInterval = TimeSpan.FromSeconds(5);
        }))
    .AddEventHandlerPipeline<ArticlePublished>(pipeline, handlerName: "article-published-handler");
```

### With a registered IConnectionMultiplexer

If your application already manages an `IConnectionMultiplexer` (StackExchange.Redis), reuse it:

```csharp
services
    .AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect("localhost:6379"))
    .AddEventBroker(x => x
        .WithRedisPersistence((redis, settings) =>
        {
            redis.UseRegisteredMultiplexer = true;
            redis.KeyPrefix = "broker"; // use a distinct prefix
        }));
```

Either `redis.ConnectionString` must be set or `redis.UseRegisteredMultiplexer` must be `true`; when it is `true`, `ConnectionString` is ignored and the `IConnectionMultiplexer` is resolved from the DI container. `redis.KeyPrefix` defaults to `"ebs_0"`; use a distinct prefix per broker instance when several share one Redis server. The `settings` argument exposes the shared `PersistentEventBrokerSettings` (polling interval, processing timeout, retention). For the full set, see the [configuration reference](05-persistent-events.md#configuration-reference).

## Starting the broker

```csharp
var provider = services.BuildServiceProvider();
provider.UsePersistentEventBroker(throwOnValidationErrors: true);
```

## Full example

```csharp
using FuncPipeline;
using M.EventBrokerSlim;
using M.EventBrokerSlim.DependencyInjection;
using M.EventBrokerSlim.Persistent;
using M.EventBrokerSlim.PersistentEvents.Redis;
using Microsoft.Extensions.DependencyInjection;

public record ArticlePublished(Guid ArticleId, string Slug);

var registry = new EventRegistry()
    .Add<ArticlePublished>("article-published");

IPipeline pipeline = PipelineBuilder.Create()
    .NewPipeline()
    .Execute(async (ArticlePublished e, CancellationToken ct) =>
    {
        Console.WriteLine($"Processing article {e.ArticleId}");
        await Task.CompletedTask;
    })
    .Build()
    .Pipelines[0];

var provider = new ServiceCollection()
    .AddSingleton(registry)
    .AddEventBroker(x => x
        .WithRedisPersistence((redis, settings) =>
        {
            redis.ConnectionString = "localhost:6379";
        }))
    .AddEventHandlerPipeline<ArticlePublished>(pipeline, handlerName: "article-published-handler")
    .BuildServiceProvider();

provider.UsePersistentEventBroker(throwOnValidationErrors: true);

var broker = provider.GetRequiredService<IEventBroker>();
await broker.Publish(new ArticlePublished(Guid.NewGuid(), "my-first-article"));

await Task.Delay(TimeSpan.FromSeconds(15));
broker.Shutdown();
```
