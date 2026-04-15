using M.EventBrokerSlim.DependencyInjection;
using M.EventBrokerSlim.Persistent;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NpgsqlTypes;
using PostgreSqlIntegrationTests;

namespace M.EventBrokerSlim.PersistentEvents.PostgreSql.Tests.Tests;

public class UnclaimedHandlingTest : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
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
    }

    [Fact]
    public async Task Scheduled_event_exceeding_unclaimed_TTL_is_dead_lettered()
    {
        await SetupScheduledEventAsync();

        // give room to maintenance task to dead-letter the event
        await Task.Delay(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        await AssertDeadLetterStatusAsync();
    }

    private async Task SetupScheduledEventAsync()
    {
        using var connection = _setup.DataSource.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO ebs_9.UnclaimedHandlingTest 
                (event_id,  event_name,  handler_name,  payload,  status,  scheduled_at,  retry_attempt_count,  retry_last_delay,  created_at,  last_updated_at,  processing_timeouts_count)
            VALUES 
                (@event_id, @event_name, @handler_name, @payload, @status, @scheduled_at, @retry_attempt_count, @retry_last_delay, @created_at, @last_updated_at, @processing_timeouts_count);
            """;
        command.Parameters.Add(new NpgsqlParameter("@event_id", NpgsqlDbType.Text) { Value = "1" });
        command.Parameters.Add(new NpgsqlParameter("@event_name", NpgsqlDbType.Text) { Value = "sample-event" });
        command.Parameters.Add(new NpgsqlParameter("@payload", NpgsqlDbType.Text) { Value = "{}" });
        command.Parameters.Add(new NpgsqlParameter("@status", NpgsqlDbType.Integer) { Value = (int)EventStatus.Scheduled });
        command.Parameters.Add(new NpgsqlParameter("@scheduled_at", NpgsqlDbType.TimestampTz) { Value = DateTime.UtcNow });
        command.Parameters.Add(new NpgsqlParameter("@retry_attempt_count", NpgsqlDbType.Integer) { Value = 0 });
        command.Parameters.Add(new NpgsqlParameter("@retry_last_delay", NpgsqlDbType.Interval) { Value = TimeSpan.Zero });
        command.Parameters.Add(new NpgsqlParameter("@created_at", NpgsqlDbType.TimestampTz) { Value = DateTime.UtcNow });
        command.Parameters.Add(new NpgsqlParameter("@last_updated_at", NpgsqlDbType.TimestampTz) { Value = DateTime.UtcNow });
        command.Parameters.Add(new NpgsqlParameter("@processing_timeouts_count", NpgsqlDbType.Integer) { Value = 0 });
        command.Parameters.Add(new NpgsqlParameter("@handler_name", NpgsqlDbType.Text) { Value = "handler" });

        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var rowsAffected = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        connection.Close();

        Assert.Equal(1, rowsAffected);
    }

    private async Task AssertDeadLetterStatusAsync()
    {
        using var connection = _setup.DataSource.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT status FROM ebs_9.{nameof(UnclaimedHandlingTest)}";
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        using NpgsqlDataReader reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        int rowsCount = 0;
        while(await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            rowsCount++;
            if(rowsCount > 1)
            {
                Assert.Fail("A single row expected");
            }

            var eventStatus = (EventStatus)reader.GetInt32(0);
            Assert.Equal(EventStatus.DeadLettered, eventStatus);
        }

        Assert.Equal(1, rowsCount);
    }

    public void Dispose()
    {
        _serviceProvider.GetRequiredService<IEventBroker>().Shutdown();
        _serviceProvider.Dispose();
    }
}
