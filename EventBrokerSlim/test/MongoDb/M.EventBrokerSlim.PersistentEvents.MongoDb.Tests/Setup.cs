using Aspire.Hosting;
using Aspire.Hosting.Testing;
using M.EventBrokerSlim.PersistentEvents.MongoDb.Tests;
using MongoDB.Driver;

[assembly: AssemblyFixture(typeof(Setup))]

namespace M.EventBrokerSlim.PersistentEvents.MongoDb.Tests;

public class Setup : IAsyncLifetime
{
    public const string DatabaseName = "ebs_0";

    private DistributedApplication? _aspireHost;
    private IMongoClient? _mongoClient;
    private string? _connectionString;

    public async ValueTask InitializeAsync()
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.MongoDbTestApp_AppHost>(TestContext.Current.CancellationToken);
        _aspireHost = await builder.BuildAsync().WaitAsync(TimeSpan.FromSeconds(20));

        await _aspireHost.StartAsync(TestContext.Current.CancellationToken);
        _connectionString = await _aspireHost.GetConnectionStringAsync("events-db");
        _mongoClient = new MongoClient(_connectionString!);
    }

    public IMongoClient MongoClient => _mongoClient ?? throw new InvalidOperationException("MongoClient not initialized. Ensure InitializeAsync has been called.");

    public string ConnectionString => _connectionString ?? throw new InvalidOperationException("ConnectionString not initialized. Ensure InitializeAsync has been called.");

    public async ValueTask DisposeAsync()
    {
        if(_aspireHost is not null)
        {
            await _aspireHost.StopAsync();
            await _aspireHost.DisposeAsync();
        }
    }
}
