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

public class RetryTest : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScope _scope;
    private readonly Setup _setup;

    public RetryTest(Setup setup)
    {
        _setup = setup;
        var builder = PipelineBuilder
            .Create()
            .NewPipeline()
            .Execute(async (SampleEvent e, IRetryPolicy retryPolicy, EventReceiver r, EventRecord record) =>
            {
                r.Add(record);
                if(record.RetryAttemptCount <= 2)
                {
                    retryPolicy.RetryAfter(TimeSpan.FromSeconds(1));
                }
            })
            .Build();
        var services = new ServiceCollection()
            .AddEventBroker(x => x.WithMongoDbPersistence((db, cfg) =>
            {
                cfg.PollingInterval = TimeSpan.FromSeconds(1);

                db.ConnectionString = setup.ConnectionString;
                db.CollectionName = nameof(RetryTest);
            }))
            .AddEventHandlerPipeline<SampleEvent>(builder.Pipelines[0], o => o.WithHandlerName("sample-event-handler"))
            .AddSingleton(EventRegistryHelper.Registry)
            .AddSingleton<EventReceiver>();

        _serviceProvider = services.BuildServiceProvider();
        _serviceProvider.UsePersistentEventBroker();
        _scope = _serviceProvider.CreateScope();
    }

    [Fact]
    public async Task Event_retried_and_successfully_handled()
    {
        var broker = _scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var receiver = _scope.ServiceProvider.GetRequiredService<EventReceiver>();
        var sampleEvent = new SampleEvent("retry");

        await broker.Publish(sampleEvent, TestContext.Current.CancellationToken);

        await receiver.WaitForEventsAsync(4, TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var receivedEvents = receiver.GetReceivedEvents();
        var receivedEvent = Assert.Single(receivedEvents, x => x.Event is SampleEvent && x.EventRecord.RetryAttemptCount == 0);
        Assert.Equal(sampleEvent, receivedEvent.Event);
        receivedEvent = Assert.Single(receivedEvents, x => x.Event is SampleEvent && x.EventRecord.RetryAttemptCount == 1);
        Assert.Equal(sampleEvent, receivedEvent.Event);
        receivedEvent = Assert.Single(receivedEvents, x => x.Event is SampleEvent && x.EventRecord.RetryAttemptCount == 2);
        Assert.Equal(sampleEvent, receivedEvent.Event);
        receivedEvent = Assert.Single(receivedEvents, x => x.Event is SampleEvent && x.EventRecord.RetryAttemptCount == 3);
        Assert.Equal(sampleEvent, receivedEvent.Event);
        await AssertStatusAsync(receivedEvent.EventRecord.Id, EventStatus.Completed);
    }

    private async Task AssertStatusAsync(string id, EventStatus expectedStatus)
    {
        var collection = _setup.MongoClient.GetDatabase(Setup.DatabaseName).GetCollection<BsonDocument>(nameof(RetryTest));
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
