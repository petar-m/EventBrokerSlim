using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using M.EventBrokerSlim.Persistent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NpgsqlTypes;

namespace M.EventBrokerSlim.PersistentEvents.PostgreSql;

internal class PostgreSqlStorage : IEventStorage
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _completeSqlUpdate;
    private readonly string _deadLetterSqlUpdate;
    private readonly string _fetchScheduledSqlQuery;
    private readonly string _retrySqlUpdate;
    private readonly string _tryClaimSqlUpdate;
    private readonly string _scheduleSqlInsert;
    private readonly ConcurrentDictionary<int, string> _insertSqlCache = new();
    private readonly ILogger<PostgreSqlStorage> _logger;

    public PostgreSqlStorage(DatabaseSettings databaseSettings, ILogger<PostgreSqlStorage>? logger = null)
    {
        _dataSource = NpgsqlDataSource.Create(databaseSettings.ConnectionString);
        _logger = logger ?? NullLogger<PostgreSqlStorage>.Instance;
        _completeSqlUpdate = $"""
            UPDATE {databaseSettings.Schema}.events
            SET status = @completed, last_updated_at = @last_updated_at
            WHERE id = @id;
            """;
        _deadLetterSqlUpdate = $"""
            UPDATE {databaseSettings.Schema}.events
            SET status = @dead_lettered, last_updated_at = @last_updated_at, last_error = @error
            WHERE id = @id;
            """;
        _fetchScheduledSqlQuery = $"""
            SELECT id, event_id, event_name, handler_name, payload, status, scheduled_at, retry_attempt_count, retry_last_delay, claimed_at, created_at, last_updated_at, last_error
            FROM {databaseSettings.Schema}.events
            WHERE status = @scheduled AND scheduled_at <= @now
            ORDER BY scheduled_at ASC
            LIMIT @batch_size;
            """;
        _retrySqlUpdate = $"""
            UPDATE {databaseSettings.Schema}.events
            SET status = @scheduled, scheduled_at = @scheduled_at, retry_attempt_count = @attempt_count, retry_last_delay = @retry_last_delay, last_updated_at = @last_updated_at, last_error = @error
            WHERE id = @id;
            """;
        _tryClaimSqlUpdate = $"""
            UPDATE {databaseSettings.Schema}.events
            SET status = @in_progress, claimed_at = @claimed_at, last_updated_at = @last_updated_at
            WHERE id = @id AND status = @scheduled;
            """;
        _scheduleSqlInsert = $$"""
            INSERT INTO {{databaseSettings.Schema}}.events (event_id, event_name, handler_name, payload, status, scheduled_at, retry_attempt_count, retry_last_delay, created_at, last_updated_at, processing_timeouts_count)
            VALUES {0};
            """;
    }

    public async Task CompleteAsync(string id, CancellationToken cancellationToken = default)
    {
        //_deadLetterSqlUpdate = $"""
        //    UPDATE {_schema}.events
        //    SET status = @dead_lettered, last_updated_at = @last_updated_at, last_error = @error
        //    WHERE id = @id;
        //    """;
        using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var updateCommand = connection.CreateCommand();
        updateCommand.CommandText = _completeSqlUpdate;
        var now = DateTimeOffset.UtcNow;
        updateCommand.Parameters.Add(new NpgsqlParameter("@completed", NpgsqlDbType.Integer) { Value = (int)EventStatus.Completed });
        updateCommand.Parameters.Add(new NpgsqlParameter("@last_updated_at", NpgsqlDbType.TimestampTz) { Value = now });
        updateCommand.Parameters.Add(new NpgsqlParameter("@id", NpgsqlDbType.Bigint) { Value = long.Parse(id) });

        _ = await updateCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeadLetterAsync(string id, string? error = null, CancellationToken cancellationToken = default)
    {
        //const string deadLetterSqlUpdate = """
        //    UPDATE ebs_0.events
        //    SET status = @dead_lettered, last_updated_at = @last_updated_at, last_error = @error
        //    WHERE id = @id;
        //    """;
        using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var updateCommand = connection.CreateCommand();
        updateCommand.CommandText = _deadLetterSqlUpdate;
        var now = DateTimeOffset.UtcNow;
        updateCommand.Parameters.Add(new NpgsqlParameter("@dead_lettered", NpgsqlDbType.Integer) { Value = (int)EventStatus.DeadLettered });
        updateCommand.Parameters.Add(new NpgsqlParameter("@last_updated_at", NpgsqlDbType.TimestampTz) { Value = now });
        updateCommand.Parameters.Add(new NpgsqlParameter("@error", NpgsqlDbType.Text) { Value = error is null ? DBNull.Value : error });
        updateCommand.Parameters.Add(new NpgsqlParameter("@id", NpgsqlDbType.Bigint) { Value = long.Parse(id) });

        _ = await updateCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IEnumerable<EventRecord>> FetchScheduledAsync(int batchSize, EventRegistry eventRegistry, CancellationToken cancellationToken = default)
    {
        //_fetchScheduledSqlQuery = $"""
        //    SELECT id, event_id, event_name, handler_name, payload, status, scheduled_at, retry_attempt_count, retry_last_delay, claimed_at, created_at, last_updated_at, last_error
        //    FROM {_schema}.events
        //    WHERE status = @scheduled AND scheduled_at <= @now
        //    ORDER BY scheduled_at ASC
        //    LIMIT @batch_size;
        //    """;
        using NpgsqlConnection connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using NpgsqlCommand selectCommand = connection.CreateCommand();
        selectCommand.CommandText = _fetchScheduledSqlQuery;
        selectCommand.Parameters.Add(new NpgsqlParameter("@scheduled", NpgsqlDbType.Integer) { Value = (int)EventStatus.Scheduled });
        selectCommand.Parameters.Add(new NpgsqlParameter("@now", NpgsqlDbType.TimestampTz) { Value = DateTimeOffset.UtcNow });
        selectCommand.Parameters.Add(new NpgsqlParameter("@batch_size", NpgsqlDbType.Integer) { Value = batchSize });
        using NpgsqlDataReader reader = await selectCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        List<EventRecord> events = new List<EventRecord>(capacity: batchSize);
        while(await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            string eventRecordId = reader.GetInt64(0).ToString();
            string eventId = reader.GetInt64(1).ToString();
            string eventName = reader.GetString(2);
            string handlerName = reader.GetString(3);
            string stringPayload = reader.GetString(4);
            EventStatus status = (EventStatus)reader.GetInt32(5);
            DateTime scheduledAt = reader.GetDateTime(6);
            int retryAttemptCount = reader.GetInt32(7);
            TimeSpan retryLastDelay = reader.GetTimeSpan(8);
            DateTime? claimedAt = reader.IsDBNull(9) ? null : reader.GetDateTime(9);
            DateTime createdAt = reader.GetDateTime(10);
            DateTime lastUpdatedAt = reader.GetDateTime(11);
            string? lastError = reader.IsDBNull(12) ? null : reader.GetString(12);

            object? deserializedEvent = EventSerializer.DeserializePayload(eventRecordId, stringPayload, eventName, eventRegistry, _logger);
            if(deserializedEvent is null)
            {
                continue;
            }

            var eventRecord = new EventRecord(
                eventRecordId,
                eventId,
                eventName,
                handlerName,
                stringPayload,
                status,
                scheduledAt,
                retryAttemptCount,
                retryLastDelay,
                claimedAt,
                createdAt,
                lastUpdatedAt,
                deserializedEvent,
                lastError);
            events.Add(eventRecord);
        }

        return events;
    }

    public async Task RetryAsync(string id, int attemptCount, TimeSpan delay, string? error = null, CancellationToken cancellationToken = default)
    {
        //_retrySqlUpdate = $"""
        //    UPDATE {_schema}.events
        //    SET status = @scheduled, scheduled_at = @scheduled_at, retry_attempt_count = @attempt_count, retry_last_delay = @retry_last_delay, last_updated_at = @last_updated_at, last_error = @error
        //    WHERE id = @id;
        //    """;
        using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var updateCommand = connection.CreateCommand();
        updateCommand.CommandText = _retrySqlUpdate;
        var now = DateTimeOffset.UtcNow;
        var scheduledAt = now.Add(delay);
        updateCommand.Parameters.Add(new NpgsqlParameter("@scheduled", NpgsqlDbType.Integer) { Value = (int)EventStatus.Scheduled });
        updateCommand.Parameters.Add(new NpgsqlParameter("@scheduled_at", NpgsqlDbType.TimestampTz) { Value = scheduledAt });
        updateCommand.Parameters.Add(new NpgsqlParameter("@attempt_count", NpgsqlDbType.Integer) { Value = attemptCount });
        updateCommand.Parameters.Add(new NpgsqlParameter("@retry_last_delay", NpgsqlDbType.Interval) { Value = delay });
        updateCommand.Parameters.Add(new NpgsqlParameter("@last_updated_at", NpgsqlDbType.TimestampTz) { Value = now });
        updateCommand.Parameters.Add(new NpgsqlParameter("@error", NpgsqlDbType.Text) { Value = error is null ? DBNull.Value : error });
        updateCommand.Parameters.Add(new NpgsqlParameter("@id", NpgsqlDbType.Bigint) { Value = long.Parse(id) });

        _ = await updateCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> TryClaimAsync(string id, CancellationToken cancellationToken = default)
    {
        //_tryClaimSqlUpdate = $"""
        //    UPDATE {_schema}.events
        //    SET status = @in_progress, claimed_at = @claimed_at, last_updated_at = @last_updated_at
        //    WHERE id = @id AND status = @scheduled;
        //    """;
        using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var updateCommand = connection.CreateCommand();
        updateCommand.CommandText = _tryClaimSqlUpdate;
        updateCommand.Parameters.Add(new NpgsqlParameter("@in_progress", NpgsqlDbType.Integer) { Value = (int)EventStatus.InProgress });
        var now = DateTimeOffset.UtcNow;
        updateCommand.Parameters.Add(new NpgsqlParameter("@claimed_at", NpgsqlDbType.TimestampTz) { Value = now });
        updateCommand.Parameters.Add(new NpgsqlParameter("@last_updated_at", NpgsqlDbType.TimestampTz) { Value = now });
        updateCommand.Parameters.Add(new NpgsqlParameter("@id", NpgsqlDbType.Bigint) { Value = long.Parse(id) });
        updateCommand.Parameters.Add(new NpgsqlParameter("@scheduled", NpgsqlDbType.Integer) { Value = (int)EventStatus.Scheduled });

        int rowsAffected = await updateCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return rowsAffected > 0;
    }

    public async Task ScheduleAsync<TEvent>(TEvent publishedEvent, string eventName, ImmutableArray<string> handlerNames, CancellationToken cancellationToken = default)
    {
        await WriteAsync(publishedEvent, eventName, handlerNames, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
    }

    public async Task ScheduleDeferredAsync<TEvent>(TEvent publishedEvent, TimeSpan deferDuration, string eventName, ImmutableArray<string> handlerNames, CancellationToken cancellationToken = default)
    {
        DateTimeOffset scheduledAt = DateTimeOffset.UtcNow.Add(deferDuration);
        await WriteAsync(publishedEvent, eventName, handlerNames, scheduledAt, cancellationToken);
    }

    private async Task WriteAsync<TEvent>(TEvent publishedEvent, string eventName, ImmutableArray<string> handlerNames, DateTimeOffset scheduledAt, CancellationToken cancellationToken)
    {
        //_scheduleSqlinsert = $"""
        //    INSERT INTO {_schema}.events (event_id, event_name, handler_name, payload, status, scheduled_at, retry_attempt_count, retry_last_delay, created_at, last_updated_at, processing_timeouts_count)
        //    VALUES {0};
        //    """;
        var command = _insertSqlCache.GetOrAdd(
            handlerNames.Length,
            length =>
            {
                StringBuilder insertValuesBuilder = new();
                for(int i = 0; i < length; i++)
                {
                    if(i > 0)
                    {
                        insertValuesBuilder.Append(", ");
                    }

                    insertValuesBuilder.Append('(');
                    insertValuesBuilder.Append($"@event_id, @event_name, @handler_name_{i}, @payload, @status, @scheduled_at, @retry_attempt_count, @retry_last_delay, @created_at, @last_updated_at, @processing_timeouts_count");
                    insertValuesBuilder.Append(')');
                }

                return string.Format(_scheduleSqlInsert, insertValuesBuilder.ToString());
            });

        string payload = EventSerializer.SerializePayload(publishedEvent);
        string eventId = Guid.NewGuid().ToString();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText = command;
        insertCommand.Parameters.Add(new NpgsqlParameter("@event_id", NpgsqlDbType.Text) { Value = eventId });
        insertCommand.Parameters.Add(new NpgsqlParameter("@event_name", NpgsqlDbType.Text) { Value = eventName });
        insertCommand.Parameters.Add(new NpgsqlParameter("@payload", NpgsqlDbType.Text) { Value = payload });
        insertCommand.Parameters.Add(new NpgsqlParameter("@status", NpgsqlDbType.Integer) { Value = (int)EventStatus.Scheduled });
        insertCommand.Parameters.Add(new NpgsqlParameter("@scheduled_at", NpgsqlDbType.TimestampTz) { Value = scheduledAt });
        insertCommand.Parameters.Add(new NpgsqlParameter("@retry_attempt_count", NpgsqlDbType.Integer) { Value = 0 });
        insertCommand.Parameters.Add(new NpgsqlParameter("@retry_last_delay", NpgsqlDbType.Interval) { Value = TimeSpan.Zero });
        insertCommand.Parameters.Add(new NpgsqlParameter("@created_at", NpgsqlDbType.TimestampTz) { Value = now });
        insertCommand.Parameters.Add(new NpgsqlParameter("@last_updated_at", NpgsqlDbType.TimestampTz) { Value = now });
        insertCommand.Parameters.Add(new NpgsqlParameter("@processing_timeouts_count", NpgsqlDbType.Integer) { Value = 0 });
        if(handlerNames.Length >= 1)
        {
            insertCommand.Parameters.Add(new NpgsqlParameter("@handler_name_0", NpgsqlDbType.Text) { Value = handlerNames[0] });
        }

        if(handlerNames.Length >= 2)
        {
            insertCommand.Parameters.Add(new NpgsqlParameter("@handler_name_1", NpgsqlDbType.Text) { Value = handlerNames[1] });
        }

        if(handlerNames.Length >= 3)
        {
            insertCommand.Parameters.Add(new NpgsqlParameter("@handler_name_2", NpgsqlDbType.Text) { Value = handlerNames[2] });
        }

        if(handlerNames.Length > 3)
        {
            for(int i = 3; i < handlerNames.Length; i++)
            {
                insertCommand.Parameters.Add(new NpgsqlParameter($"@handler_name_{i}", NpgsqlDbType.Text) { Value = handlerNames[i] });
            }
        }

        await insertCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
