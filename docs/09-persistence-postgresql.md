---
layout: default
title: Persistent Events: PostgreSQL
nav_order: 9
---

# Persistent Events: PostgreSQL

PostgreSQL is a relational database server. This backend stores events in a table within a configurable schema, accessed over the network. See [Choosing a backend](05-persistent-events.md#choosing-a-backend) to compare the alternatives.

## Install

```
dotnet add package M.EventBrokerSlim.PersistentEvents.PostgreSql
```

## Schema setup

The events table and its indexes must exist before the broker starts. Create them with `CreateEventsTable()` on a `DatabaseSettings` instance:

```csharp
var db = new DatabaseSettings { ConnectionString = "Host=localhost;Database=myapp;Username=myuser;Password=secret" };
db.CreateEventsTable();
```

The call is idempotent (`CREATE ... IF NOT EXISTS`) and creates the schema, sequence, table, and partial indexes named by `db.Schema` and `db.Table` (defaults `"ebs_0"` and `"events"`). It needs DDL permissions. In development you can run it at startup; in production, run it as part of deployment or migration rather than on every start.

To create the schema by hand instead, apply the [`initialize_db.sql`](https://github.com/petar-m/EventBrokerSlim/blob/main/EventBrokerSlim/src/M.EventBrokerSlim.PersistentEvents.PostgreSql/initialize_db.sql) script from the GitHub repository. It is not shipped in the NuGet package. With a migration tool (FluentMigrator, DbUp, EF Core), embed it as a migration step.

## Configuration

```csharp
var registry = new EventRegistry()
    .Add<ArticlePublished>("article-published");

services
    .AddSingleton(registry)
    .AddEventBroker(x => x
        .WithPostgreSqlPersistence((db, settings) =>
        {
            db.ConnectionString = "Host=localhost;Database=myapp;Username=myuser;Password=secret";
            db.Schema = "ebs_0"; // optional, default: "ebs_0"
            db.Table = "events"; // optional, default: "events"

            settings.PollingInterval = TimeSpan.FromSeconds(5);
        }))
    .AddEventHandlerPipeline<ArticlePublished>(pipeline, handlerName: "article-published-handler");
```

`db.ConnectionString` is required. `db.Schema` and `db.Table` must match the schema and table created during setup; they default to `"ebs_0"` and `"events"`. When several broker instances share one database, give each a unique schema or table name. The `settings` argument exposes the shared `PersistentEventBrokerSettings` (polling interval, processing timeout, retention). For the full set, see the [configuration reference](05-persistent-events.md#configuration-reference).

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
using M.EventBrokerSlim.PersistentEvents.PostgreSql;
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
        .WithPostgreSqlPersistence((db, settings) =>
        {
            db.ConnectionString = "Host=localhost;Database=myapp;Username=myuser;Password=secret";
            db.CreateEventsTable();
        }))
    .AddEventHandlerPipeline<ArticlePublished>(pipeline, handlerName: "article-published-handler")
    .BuildServiceProvider();

provider.UsePersistentEventBroker(throwOnValidationErrors: true);

var broker = provider.GetRequiredService<IEventBroker>();
await broker.Publish(new ArticlePublished(Guid.NewGuid(), "my-first-article"));

await Task.Delay(TimeSpan.FromSeconds(15));
broker.Shutdown();
```
