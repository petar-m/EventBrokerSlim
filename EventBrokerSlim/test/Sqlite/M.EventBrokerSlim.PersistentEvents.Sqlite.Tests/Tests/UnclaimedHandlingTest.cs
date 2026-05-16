using System;
using System.Threading.Tasks;
using M.EventBrokerSlim.DependencyInjection;
using M.EventBrokerSlim.Persistent;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using SqliteIntegrationTests;

namespace M.EventBrokerSlim.PersistentEvents.Sqlite.Tests.Tests;

public class UnclaimedHandlingTest : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScope _scope;
    private readonly string _connectionString;

    public UnclaimedHandlingTest(Setup setup)
    {
        _connectionString = setup.GetConnectionString();
        var services = new ServiceCollection()
            .AddEventBroker(x => x.WithSqlitePersistence((db, cfg) =>
            {
                cfg.UnclaimedTtl = TimeSpan.FromSeconds(2);
                cfg.DeadLetterUnclaimedExecuteInterval =
                new Jitter(
                    initial: new Jitter.Periodic(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(0)),
                    regular: new Jitter.Periodic(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)));

                db.ConnectionString = _connectionString;
                db.Table = nameof(UnclaimedHandlingTest);
                db.CreateEventsTable();
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
        using var connection = new SqliteConnection(_connectionString);
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT status FROM {nameof(UnclaimedHandlingTest)}";
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        using SqliteDataReader reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        int rowsCount = 0;
        while(await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            rowsCount++;
            if(rowsCount > 1)
            {
                Assert.Fail("A single row expected");
            }

            var eventStatus = (EventStatus)(long)reader.GetValue(0);
            Assert.Equal(EventStatus.DeadLettered, eventStatus);
        }

        Assert.Equal(1, rowsCount);
    }

    public void Dispose()
    {
        _serviceProvider.GetRequiredService<IEventBroker>().Shutdown();
        _scope.Dispose();
        _serviceProvider.Dispose();
    }
}
