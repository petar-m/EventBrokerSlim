using M.EventBrokerSlim.DependencyInjection;
using M.EventBrokerSlim.Persistent;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NpgsqlTypes;
using PostgreSqlIntegrationTests;

namespace M.EventBrokerSlim.PersistentEvents.PostgreSql.Tests.Tests;

public class DeadLetteredAndCompletedHandlingTest : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly Setup _setup;

    public DeadLetteredAndCompletedHandlingTest(Setup setup)
    {
        _setup = setup;
        var services = new ServiceCollection()
            .AddEventBroker(x => x.WithPostgreSqlPersistence((db, cfg) =>
            {
                cfg.CompletedRecordTtl = TimeSpan.FromSeconds(2);
                cfg.DeadLetteredRecordTtl = TimeSpan.FromSeconds(2);
                cfg.DeleteCompletedAndDeadLetteredExceedingTtlExecuteInterval =
                new Jitter(
                    initial: new Jitter.Periodic(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(0)),
                    regular: new Jitter.Periodic(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)));

                db.ConnectionString = setup.ConnectionString;
                db.Schema = "ebs_10";
                db.Table = nameof(DeadLetteredAndCompletedHandlingTest);
                db.CreateEventsTable();
            }))
            .AddSingleton(EventRegistryHelper.Registry);

        _serviceProvider = services.BuildServiceProvider();
        _serviceProvider.UsePersistentEventBroker();
    }

    [Fact]
    public async Task Scheduled_event_exceeding_unclaimed_TTL_is_dead_lettered()
    {
        await SetupEventAsync(EventStatus.DeadLettered);
        await SetupEventAsync(EventStatus.Completed);

        // give room to maintenance task to delete the events
        await Task.Delay(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        await AssertNoRowsAsync();
    }

    private async Task SetupEventAsync(EventStatus status)
    {
        using var connection = _setup.DataSource.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO ebs_10.DeadLetteredAndCompletedHandlingTest 
                (event_id,  event_name,  handler_name,  payload,  status,  scheduled_at,  retry_attempt_count,  retry_last_delay,  created_at,  last_updated_at,  processing_timeouts_count)
            VALUES 
                (@event_id, @event_name, @handler_name, @payload, @status, @scheduled_at, @retry_attempt_count, @retry_last_delay, @created_at, @last_updated_at, @processing_timeouts_count);
            """;
        command.Parameters.Add(new NpgsqlParameter("@event_id", NpgsqlDbType.Text) { Value = Guid.NewGuid().ToString() });
        command.Parameters.Add(new NpgsqlParameter("@event_name", NpgsqlDbType.Text) { Value = "sample-event" });
        command.Parameters.Add(new NpgsqlParameter("@payload", NpgsqlDbType.Text) { Value = "{}" });
        command.Parameters.Add(new NpgsqlParameter("@status", NpgsqlDbType.Integer) { Value = (int)status });
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

    private async Task AssertNoRowsAsync()
    {
        using var connection = _setup.DataSource.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM ebs_10.{nameof(DeadLetteredAndCompletedHandlingTest)}";
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var count = (long?)await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, count);
    }
    public void Dispose()
    {
        _serviceProvider.GetRequiredService<IEventBroker>().Shutdown();
        _serviceProvider.Dispose();
    }
}
