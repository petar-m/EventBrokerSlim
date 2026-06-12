# Persistent Events: LiteDB

LiteDB is an embedded, serverless document database for .NET. It stores everything in a single `.db` file, with no SQL schema and no server to run. See [Choosing a backend](05-persistent-events.md#choosing-a-backend) to compare the alternatives.

## Install

```
dotnet add package M.EventBrokerSlim.PersistentEvents.LiteDb
```

## Schema setup

None required. LiteDB creates the collection on first use.

## Configuration

### With a connection string

```csharp
var registry = new EventRegistry()
    .Add<ArticlePublished>("article-published");

services
    .AddSingleton(registry)
    .AddEventBroker(x => x
        .WithLiteDbPersistence((db, settings) =>
        {
            db.ConnectionString = "Filename=events.db";
            db.Collection = "events"; // optional, default: "events"

            settings.PollingInterval = TimeSpan.FromSeconds(5);
        }))
    .AddEventHandlerPipeline<ArticlePublished>(pipeline, handlerName: "article-published-handler");
```

### With an existing LiteDatabase instance

If your application already manages a `LiteDatabase` instance, pass it directly. The event broker uses it as-is:

```csharp
var liteDb = new LiteDatabase("Filename=myapp.db");

services
    .AddSingleton(liteDb)
    .AddEventBroker(x => x
        .WithLiteDbPersistence((db, settings) =>
        {
            db.LiteDbInstance = liteDb;
            db.Collection = "broker_events"; // use a distinct collection name
        }));
```

Either `db.ConnectionString` or `db.LiteDbInstance` must be set; if both are set, `LiteDbInstance` wins and the connection string is ignored. `db.Collection` defaults to `"events"`; when several broker instances share one database file, give each a unique collection name. The `settings` argument exposes the shared `PersistentEventBrokerSettings` (polling interval, processing timeout, retention). For the full set, see the [configuration reference](05-persistent-events.md#configuration-reference).

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
using M.EventBrokerSlim.PersistentEvents.LiteDb;
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
        .WithLiteDbPersistence((db, settings) =>
        {
            db.ConnectionString = "Filename=events.db";
        }))
    .AddEventHandlerPipeline<ArticlePublished>(pipeline, handlerName: "article-published-handler")
    .BuildServiceProvider();

provider.UsePersistentEventBroker(throwOnValidationErrors: true);

var broker = provider.GetRequiredService<IEventBroker>();
await broker.Publish(new ArticlePublished(Guid.NewGuid(), "my-first-article"));

await Task.Delay(TimeSpan.FromSeconds(15));
broker.Shutdown();
```
