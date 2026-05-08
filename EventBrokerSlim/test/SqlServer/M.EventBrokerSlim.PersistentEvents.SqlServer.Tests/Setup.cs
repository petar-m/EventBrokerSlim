using Aspire.Hosting;
using Aspire.Hosting.Testing;
using M.EventBrokerSlim.PersistentEvents.SqlServer.Tests;

[assembly: AssemblyFixture(typeof(Setup))]

namespace M.EventBrokerSlim.PersistentEvents.SqlServer.Tests;

public class Setup : IAsyncLifetime
{
    private DistributedApplication? _aspireHost;
    private string? _connectionString;

    public async ValueTask InitializeAsync()
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.SqlServerTestApp_AppHost>(TestContext.Current.CancellationToken);
        _aspireHost = await builder.BuildAsync().WaitAsync(TimeSpan.FromSeconds(20));

        await _aspireHost.StartAsync(TestContext.Current.CancellationToken);
        _connectionString = await _aspireHost.GetConnectionStringAsync("events");
    }

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
