# Persistent Events: SQLite

SQLite is an embedded database that stores everything in a single file. No server to run, no network connection, no runtime dependency beyond the file itself. See [Choosing a backend](05-persistent-events.md#choosing-a-backend) to compare the alternatives.

## Install

```
dotnet add package M.EventBrokerSlim.PersistentEvents.Sqlite
```

## Schema setup

The events table and its indexes must exist before the broker starts. Create them with `CreateEventsTable()` on a `DatabaseSettings` instance:

```csharp
var db = new DatabaseSettings { ConnectionString = "Data Source=events.db" };
db.CreateEventsTable();
```

The call is idempotent (`CREATE TABLE IF NOT EXISTS`) and enables WAL journal mode so reads run concurrently with writes. It needs DDL permissions. In development you can run it at startup; in production, run it as part of deployment or migration rather than on every start.

To create the schema by hand instead, apply the [`initialize_db.sql`](https://github.com/petar-m/EventBrokerSlim/blob/main/EventBrokerSlim/src/M.EventBrokerSlim.PersistentEvents.Sqlite/initialize_db.sql) script from the GitHub repository. It is not shipped in the NuGet package.

## Configuration

```csharp
var registry = new EventRegistry()
    .Add<ArticlePublished>("article-published");

services
    .AddSingleton(registry)
    .AddEventBroker(x => x
        .WithSqlitePersistence((db, settings) =>
        {
            db.ConnectionString = "Data Source=events.db";
            db.Table = "events"; // optional, default: "events"

            settings.PollingInterval = TimeSpan.FromSeconds(5);
        }))
    .AddEventHandlerPipeline<ArticlePublished>(pipeline, handlerName: "article-published-handler");
```

`db.ConnectionString` is required. `db.Table` defaults to `"events"`; use a unique table name per broker instance when several share one database file. The `settings` argument exposes the shared `PersistentEventBrokerSettings` (polling interval, processing timeout, retention). For the full set, see the [configuration reference](05-persistent-events.md#configuration-reference).

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
using M.EventBrokerSlim.PersistentEvents.Sqlite;
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
        .WithSqlitePersistence((db, settings) =>
        {
            db.ConnectionString = "Data Source=events.db";
            db.CreateEventsTable();
        }))
    .AddEventHandlerPipeline<ArticlePublished>(pipeline, handlerName: "article-published-handler")
    .BuildServiceProvider();

provider.UsePersistentEventBroker(throwOnValidationErrors: true);

var broker = provider.GetRequiredService<IEventBroker>();
await broker.Publish(new ArticlePublished(Guid.NewGuid(), "my-first-article"));

// Allow time for the polling loop to pick up and dispatch the event
await Task.Delay(TimeSpan.FromSeconds(15));
broker.Shutdown();
```
