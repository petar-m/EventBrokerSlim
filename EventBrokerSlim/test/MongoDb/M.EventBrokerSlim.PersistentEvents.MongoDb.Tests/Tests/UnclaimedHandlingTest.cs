using M.EventBrokerSlim.DependencyInjection;
using M.EventBrokerSlim.Persistent;
using M.EventBrokerSlim.PersistentEvents.MongoDb;
using M.EventBrokerSlim.PersistentEvents.MongoDb.Tests;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDbIntegrationTests;

namespace M.EventBrokerSlim.PersistentEvents.MongoDb.Tests.Tests;

public class UnclaimedHandlingTest : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScope _scope;
    private readonly Setup _setup;

    public UnclaimedHandlingTest(Setup setup)
    {
        _setup = setup;
        var services = new ServiceCollection()
            .AddEventBroker(x => x.WithMongoDbPersistence((db, cfg) =>
            {
                cfg.UnclaimedTtl = TimeSpan.FromSeconds(2);
                cfg.DeadLetterUnclaimedExecuteInterval =
                new Jitter(
                    initial: new Jitter.Periodic(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(0)),
                    regular: new Jitter.Periodic(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)));

                db.ConnectionString = setup.ConnectionString;
                db.CollectionName = nameof(UnclaimedHandlingTest);
            }))
            .AddSingleton(EventRegistryHelper.Registry);

        services.AddEventHandlerPipeline<SampleEvent>(NullPipeline.Instance, handlerName: "null-pipeline");

        _serviceProvider = services.BuildServiceProvider();
        _scope = _serviceProvider.CreateScope();
        _scope.ServiceProvider.UsePersistentEventBroker();
    }

    [Fact]
    public async Task Scheduled_event_exceeding_unclaimed_TTL_is_dead_lettered()
    {
        var eventBroker = _scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var sampleEvent = new SampleEvent("unclaimed");

        await eventBroker.Publish(sampleEvent, TestContext.Current.CancellationToken);

        // give room to maintenance task to dead-letter the event
        await Task.Delay(TimeSpan.FromSeconds(4), TestContext.Current.CancellationToken);
        await AssertDeadLetterStatusAsync();
    }

    private async Task AssertDeadLetterStatusAsync()
    {
        var collection = _setup.MongoClient.GetDatabase(Setup.DatabaseName).GetCollection<BsonDocument>(nameof(UnclaimedHandlingTest));
        var docs = await collection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(docs);
        Assert.Equal((int)EventStatus.DeadLettered, docs[0]["status"].AsInt32);
    }

    public void Dispose()
    {
        _serviceProvider.GetRequiredService<IEventBroker>().Shutdown();
        _serviceProvider.Dispose();
    }
}
