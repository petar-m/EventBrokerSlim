---
layout: default
title: Persistent Events: MongoDB
nav_order: 8
---

# Persistent Events: MongoDB

MongoDB is a document database that runs as a server, accessed over the network. This backend stores each event as a document in a collection. See [Choosing a backend](05-persistent-events.md#choosing-a-backend) to compare the alternatives.

## Install

```
dotnet add package M.EventBrokerSlim.PersistentEvents.MongoDb
```

## Schema setup

None required. The collection and its indexes are created automatically on first use.

## Configuration

### With a connection string

```csharp
var registry = new EventRegistry()
    .Add<ArticlePublished>("article-published");

services
    .AddSingleton(registry)
    .AddEventBroker(x => x
        .WithMongoDbPersistence((db, settings) =>
        {
            db.ConnectionString = "mongodb://localhost:27017";
            db.DatabaseName = "ebs_0";    // optional, default: "ebs_0"
            db.CollectionName = "events"; // optional, default: "events"

            settings.PollingInterval = TimeSpan.FromSeconds(5);
        }))
    .AddEventHandlerPipeline<ArticlePublished>(pipeline, handlerName: "article-published-handler");
```

### With an existing IMongoDatabase instance

If your application already configures a MongoDB connection, pass the `IMongoDatabase` directly. The event broker uses it as-is:

```csharp
IMongoDatabase mongoDatabase = mongoClient.GetDatabase("myapp");

services
    .AddEventBroker(x => x
        .WithMongoDbPersistence((db, settings) =>
        {
            db.MongoDatabase = mongoDatabase;
            db.CollectionName = "broker_events"; // use a distinct collection name
        }));
```

Either `db.ConnectionString` or `db.MongoDatabase` must be set; if `MongoDatabase` is set, `ConnectionString` and `DatabaseName` are ignored. `db.DatabaseName` defaults to `"ebs_0"` and `db.CollectionName` to `"events"`; when several broker instances share one database, give each a unique collection name (or a separate database). The `settings` argument exposes the shared `PersistentEventBrokerSettings` (polling interval, processing timeout, retention). For the full set, see the [configuration reference](05-persistent-events.md#configuration-reference).

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
using M.EventBrokerSlim.PersistentEvents.MongoDb;
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
        .WithMongoDbPersistence((db, settings) =>
        {
            db.ConnectionString = "mongodb://localhost:27017";
            db.DatabaseName = "myapp";
            db.CollectionName = "broker_events";
        }))
    .AddEventHandlerPipeline<ArticlePublished>(pipeline, handlerName: "article-published-handler")
    .BuildServiceProvider();

provider.UsePersistentEventBroker(throwOnValidationErrors: true);

var broker = provider.GetRequiredService<IEventBroker>();
await broker.Publish(new ArticlePublished(Guid.NewGuid(), "my-first-article"));

await Task.Delay(TimeSpan.FromSeconds(15));
broker.Shutdown();
```
