using FuncPipeline;
using M.EventBrokerSlim.DependencyInjection;
using M.EventBrokerSlim.Persistent;
using M.EventBrokerSlim.PersistentEvents.MongoDb;
using M.EventBrokerSlim.PersistentEvents.MongoDb.Tests;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDbIntegrationTests;

namespace M.EventBrokerSlim.PersistentEvents.MongoDb.Tests.Tests;

public class SingleEventTest : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScope _scope;
    private readonly Setup _setup;

    public SingleEventTest(Setup setup)
    {
        _setup = setup;
        var builder = PipelineBuilder
            .Create()
            .NewPipeline()
            .Execute(async (SampleEvent e, EventReceiver r, EventRecord record) => r.Add(record))
            .Build();
        var services = new ServiceCollection()
            .AddEventBroker(x => x.WithMongoDbPersistence((db, cfg) =>
            {
                db.ConnectionString = setup.ConnectionString;
                db.CollectionName = nameof(SingleEventTest);
            }))
            .AddEventHandlerPipeline<SampleEvent>(builder.Pipelines[0], o => o.WithHandlerName("sample-event-handler"))
            .AddSingleton(EventRegistryHelper.Registry)
            .AddSingleton<EventReceiver>();

        _serviceProvider = services.BuildServiceProvider();
        _serviceProvider.UsePersistentEventBroker();
        _scope = _serviceProvider.CreateScope();
    }

    [Fact]
    public async Task Event_sent_and_received()
    {
        var broker = _scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var receiver = _scope.ServiceProvider.GetRequiredService<EventReceiver>();
        var sampleEvent = new SampleEvent("hello from mongodb handler!");

        await broker.Publish(sampleEvent, TestContext.Current.CancellationToken);

        await receiver.WaitForEventsAsync(1, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var receivedEvents = receiver.GetReceivedEvents();
        Assert.Single(receivedEvents);
        Assert.Equal(sampleEvent, receivedEvents[0].Event);
        await AssertStatusAsync(receivedEvents[0].EventRecord.Id, EventStatus.Completed);
    }

    private async Task AssertStatusAsync(string id, EventStatus expectedStatus)
    {
        var collection = _setup.MongoClient.GetDatabase(Setup.DatabaseName).GetCollection<BsonDocument>(nameof(SingleEventTest));
        var filter = Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(id));
        var doc = await collection.Find(filter).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(doc);
        Assert.Equal((int)expectedStatus, doc["status"].AsInt32);
    }

    public void Dispose()
    {
        _scope.ServiceProvider.GetRequiredService<IEventBroker>().Shutdown();
        _scope.Dispose();
        _serviceProvider.Dispose();
    }
}
