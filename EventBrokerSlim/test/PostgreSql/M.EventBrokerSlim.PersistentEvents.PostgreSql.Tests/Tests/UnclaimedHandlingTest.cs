using M.EventBrokerSlim.DependencyInjection;
using M.EventBrokerSlim.Persistent;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PostgreSqlIntegrationTests;

namespace M.EventBrokerSlim.PersistentEvents.PostgreSql.Tests.Tests;

public class UnclaimedHandlingTest : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScope _scope;
    private readonly Setup _setup;

    public UnclaimedHandlingTest(Setup setup)
    {
        _setup = setup;
        var services = new ServiceCollection()
            .AddEventBroker(x => x.WithPostgreSqlPersistence((db, cfg) =>
            {
                cfg.UnclaimedTtl = TimeSpan.FromSeconds(2);
                cfg.DeadLetterUnclaimedExecuteInterval =
                new Jitter(
                    initial: new Jitter.Periodic(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(0)),
                    regular: new Jitter.Periodic(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)));

                db.ConnectionString = setup.ConnectionString;
                db.Schema = "ebs_9";
                db.Table = nameof(UnclaimedHandlingTest);
                db.CreateEventsTable();
            }))
            .AddSingleton(EventRegistryHelper.Registry);

        _serviceProvider = services.BuildServiceProvider();
        _serviceProvider.UsePersistentEventBroker();
        _scope = _serviceProvider.CreateScope();
    }

    [Fact]
    public async Task Scheduled_event_exceeding_unclaimed_TTL_is_dead_lettered()
    {
        var broker = _scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var sampleEvent = new SampleEvent("to be unclaimed");

        await broker.Publish(sampleEvent, TestContext.Current.CancellationToken);

        // give room to maintenance task to dead-letter the event
        await Task.Delay(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        await AssertDeadLetterStatusAsync();
    }

    private async Task AssertDeadLetterStatusAsync()
    {
        using var connection = _setup.DataSource.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT status FROM ebs_9.{nameof(UnclaimedHandlingTest)}";
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        using NpgsqlDataReader reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        int rowsCount = 0;
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            rowsCount++;
            if(rowsCount > 1)
            {
                Assert.Fail("A single row expected");
            }

            var eventStatus = (EventStatus)reader.GetInt32(0);
            Assert.Equal(EventStatus.DeadLettered, eventStatus);
        }
    }

    public void Dispose()
    {
        _scope.ServiceProvider.GetRequiredService<IEventBroker>().Shutdown();
        _scope.Dispose();
        _serviceProvider.Dispose();
    }
}
