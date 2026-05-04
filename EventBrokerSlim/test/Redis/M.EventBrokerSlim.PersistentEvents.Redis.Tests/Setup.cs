using Aspire.Hosting;
using Aspire.Hosting.Testing;
using M.EventBrokerSlim.PersistentEvents.Redis.Tests;
using StackExchange.Redis;

[assembly: AssemblyFixture(typeof(Setup))]

namespace M.EventBrokerSlim.PersistentEvents.Redis.Tests;

public class Setup : IAsyncLifetime
{
    private DistributedApplication? _aspireHost;
    private ConnectionMultiplexer? _dataSource;
    private string? _connectionString;

    public async ValueTask InitializeAsync()
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.RedisTestApp_AppHost>(TestContext.Current.CancellationToken);
        _aspireHost = await builder.BuildAsync().WaitAsync(TimeSpan.FromSeconds(20));

        await _aspireHost.StartAsync(TestContext.Current.CancellationToken);
        _connectionString = await _aspireHost.GetConnectionStringAsync("events-db");
        _dataSource = await ConnectionMultiplexer.ConnectAsync(_connectionString!);
    }

    public ConnectionMultiplexer DataSource => _dataSource ?? throw new InvalidOperationException("DataSource not initialized. Ensure InitializeAsync has been called.");

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
