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

namespace M.EventBrokerSlim.PersistentEvents.PostgreSql.Internal;

internal class PostgreSqlStorage : IEventStorage
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _completeSingleSqlUpdate;
    private readonly string _deadLetterSingleSqlUpdate;
    private readonly string _fetchScheduledSqlQuery;
    private readonly string _retrySingleSqlUpdate;
    private readonly string _tryClaimSingleSqlUpdate;
    private readonly string _scheduleSqlInsert;
    private readonly string _rescheduleClaimedExceedingProcessingTimeoutUpdateSql;
    private readonly string _deadLetterUnclaimedSqlUpdate;
    private readonly string _deleteDeadLetteredAndCompletedExceededTtlSql;
    private readonly ConcurrentDictionary<int, string> _insertSqlCache = new();
    private readonly PersistentEventBrokerSettings _eventBrokerSettings;
    private readonly ILogger<PostgreSqlStorage> _logger;

    public PostgreSqlStorage(DatabaseSettings databaseSettings, PersistentEventBrokerSettings eventBrokerSettings, ILogger<PostgreSqlStorage> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseSettings.ConnectionString);
        _dataSource = NpgsqlDataSource.Create(databaseSettings.ConnectionString);
        _eventBrokerSettings = eventBrokerSettings;
        _logger = logger ?? NullLogger<PostgreSqlStorage>.Instance;
        _completeSingleSqlUpdate = $"""
            UPDATE {databaseSettings.Schema}.events
            SET status = @completed, last_updated_at = @last_updated_at
            WHERE id = @id;
            """;
        _deadLetterSingleSqlUpdate = $"""
            UPDATE {databaseSettings.Schema}.events
            SET status = @dead_lettered, last_updated_at = @last_updated_at, last_error = @error
            WHERE id = @id;
            """;
        _fetchScheduledSqlQuery = $"""
            SELECT id, last_updated_at, event_name, handler_name
            FROM {databaseSettings.Schema}.events
            WHERE status = @scheduled AND scheduled_at <= @now
            ORDER BY scheduled_at ASC
            LIMIT @batch_size;
            """;
        _retrySingleSqlUpdate = $"""
            UPDATE {databaseSettings.Schema}.events
            SET status = @scheduled, scheduled_at = @scheduled_at, retry_attempt_count = @attempt_count, retry_last_delay = @retry_last_delay, last_updated_at = @last_updated_at, last_error = @error
            WHERE id = @id;
            """;
        _tryClaimSingleSqlUpdate = $"""
            UPDATE {databaseSettings.Schema}.events
            SET status = @in_progress, claimed_at = @claimed_at, last_updated_at = @last_updated_at
            WHERE id = @candidate_id AND status = @scheduled AND last_updated_at = @candidate_last_updated_at
            RETURNING id, event_id, event_name, handler_name, payload, status, scheduled_at, retry_attempt_count, retry_last_delay, claimed_at, created_at, last_updated_at, last_error, processing_timeouts_count;
            """;
        _scheduleSqlInsert = $$"""
            INSERT INTO {{databaseSettings.Schema}}.events (event_id, event_name, handler_name, payload, status, scheduled_at, retry_attempt_count, retry_last_delay, created_at, last_updated_at, processing_timeouts_count)
            VALUES {0};
            """;
        _rescheduleClaimedExceedingProcessingTimeoutUpdateSql = $"""
           UPDATE {databaseSettings.Schema}.events
           SET
               status = CASE
                   WHEN processing_timeouts_count >= @max_processing_timeouts THEN @dead_lettered
                   ELSE @scheduled
               END,
               scheduled_at = CASE
                   WHEN processing_timeouts_count < @max_processing_timeouts THEN @scheduled_at
                   ELSE scheduled_at
               END,
               last_updated_at = @last_updated_at,
               last_error = CASE
                   WHEN processing_timeouts_count >= @max_processing_timeouts THEN @error
                   ELSE last_error
               END,
               processing_timeouts_count = CASE
                   WHEN processing_timeouts_count < @max_processing_timeouts THEN processing_timeouts_count + 1
                   ELSE processing_timeouts_count
               END,
               claimed_at = CASE
                   WHEN processing_timeouts_count < @max_processing_timeouts THEN NULL
                   ELSE claimed_at
               END
           WHERE status = @in_progress AND claimed_at <= @claimed_before
           """;
        _deadLetterUnclaimedSqlUpdate = $"""
            UPDATE {databaseSettings.Schema}.events SET
                status = @dead_lettered,
                last_updated_at = @last_updated_at,
                last_error = @error
            WHERE status = @scheduled AND scheduled_at <= @scheduled_before;
            """;
        _deleteDeadLetteredAndCompletedExceededTtlSql = $"""
            DELETE FROM {databaseSettings.Schema}.events
            WHERE 
            (status = @completed AND last_updated_at <= @completed_before) OR 
            (status = @dead_lettered AND last_updated_at <= @dead_lettered_before);
            """;
    }

    public async Task CompleteAsync(string id, CancellationToken cancellationToken = default)
    {
        using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var updateCommand = connection.CreateCommand();
        updateCommand.CommandText = _completeSingleSqlUpdate;
        var now = DateTimeOffset.UtcNow;
        updateCommand.Parameters.Add(new NpgsqlParameter("@completed", NpgsqlDbType.Integer) { Value = (int)EventStatus.Completed });
        updateCommand.Parameters.Add(new NpgsqlParameter("@last_updated_at", NpgsqlDbType.TimestampTz) { Value = now });
        updateCommand.Parameters.Add(new NpgsqlParameter("@id", NpgsqlDbType.Bigint) { Value = long.Parse(id) });

        _ = await updateCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeadLetterAsync(string id, string? error = null, CancellationToken cancellationToken = default)
    {
        using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var updateCommand = connection.CreateCommand();
        updateCommand.CommandText = _deadLetterSingleSqlUpdate;
        var now = DateTimeOffset.UtcNow;
        updateCommand.Parameters.Add(new NpgsqlParameter("@dead_lettered", NpgsqlDbType.Integer) { Value = (int)EventStatus.DeadLettered });
        updateCommand.Parameters.Add(new NpgsqlParameter("@last_updated_at", NpgsqlDbType.TimestampTz) { Value = now });
        updateCommand.Parameters.Add(new NpgsqlParameter("@error", NpgsqlDbType.Text) { Value = error is null ? DBNull.Value : error });
        updateCommand.Parameters.Add(new NpgsqlParameter("@id", NpgsqlDbType.Bigint) { Value = long.Parse(id) });

        _ = await updateCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IEnumerable<ScheduledEventRecord>> FetchScheduledAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        using NpgsqlConnection connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using NpgsqlCommand selectCommand = connection.CreateCommand();
        selectCommand.CommandText = _fetchScheduledSqlQuery;
        selectCommand.Parameters.Add(new NpgsqlParameter("@scheduled", NpgsqlDbType.Integer) { Value = (int)EventStatus.Scheduled });
        selectCommand.Parameters.Add(new NpgsqlParameter("@now", NpgsqlDbType.TimestampTz) { Value = DateTimeOffset.UtcNow });
        selectCommand.Parameters.Add(new NpgsqlParameter("@batch_size", NpgsqlDbType.Integer) { Value = batchSize });
        using NpgsqlDataReader reader = await selectCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var events = new List<ScheduledEventRecord>(capacity: batchSize);
        while(await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            string eventRecordId = reader.GetInt64(0).ToString();
            DateTime lastUpdatedAt = reader.GetDateTime(1);
            string eventName = reader.GetString(2);
            string handlerName = reader.GetString(3);
            var eventRecord = new ScheduledEventRecord(eventRecordId, lastUpdatedAt, eventName, handlerName);
            events.Add(eventRecord);
        }

        return events;
    }

    public async Task RetryAsync(string id, int attemptCount, TimeSpan delay, string? error = null, CancellationToken cancellationToken = default)
    {
        using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var updateCommand = connection.CreateCommand();
        updateCommand.CommandText = _retrySingleSqlUpdate;
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

    public async Task<EventRecord> TryClaimAsync(ScheduledEventRecord scheduledEventRecord, EventRegistry eventRegistry, CancellationToken cancellationToken = default)
    {
        using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var updateCommand = connection.CreateCommand();
        updateCommand.CommandText = _tryClaimSingleSqlUpdate;
        updateCommand.Parameters.Add(new NpgsqlParameter("@in_progress", NpgsqlDbType.Integer) { Value = (int)EventStatus.InProgress });
        var now = DateTimeOffset.UtcNow;
        updateCommand.Parameters.Add(new NpgsqlParameter("@claimed_at", NpgsqlDbType.TimestampTz) { Value = now });
        updateCommand.Parameters.Add(new NpgsqlParameter("@last_updated_at", NpgsqlDbType.TimestampTz) { Value = now });
        updateCommand.Parameters.Add(new NpgsqlParameter("@candidate_id", NpgsqlDbType.Bigint) { Value = long.Parse(scheduledEventRecord.Id) });
        updateCommand.Parameters.Add(new NpgsqlParameter("@candidate_last_updated_at", NpgsqlDbType.TimestampTz) { Value = scheduledEventRecord.LastUpdatedAt });
        updateCommand.Parameters.Add(new NpgsqlParameter("@scheduled", NpgsqlDbType.Integer) { Value = (int)EventStatus.Scheduled });

        NpgsqlDataReader reader = await updateCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        if(await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            string eventRecordId = reader.GetInt64(0).ToString();
            string eventId = reader.GetString(1);
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
            int processingTimeoutsCount = reader.GetInt32(13);

            object? deserializedEvent = EventSerializer.DeserializePayload(eventRecordId, stringPayload, eventName, eventRegistry, _logger);
            if(deserializedEvent is null)
            {
                return EventRecord.Empty;
            }

            return new EventRecord(
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
                lastError,
                processingTimeoutsCount);
        }

        return EventRecord.Empty;
    }

    public async Task RescheduleClaimedExceedingProcessingTimeoutAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var claimedBefore = now.Subtract(_eventBrokerSettings.ProcessingTimeout);

        using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rescheduleCommand = connection.CreateCommand();
        rescheduleCommand.CommandText = _rescheduleClaimedExceedingProcessingTimeoutUpdateSql;
        rescheduleCommand.Parameters.Add(new NpgsqlParameter("@scheduled", NpgsqlDbType.Integer) { Value = (int)EventStatus.Scheduled });
        rescheduleCommand.Parameters.Add(new NpgsqlParameter("@in_progress", NpgsqlDbType.Integer) { Value = (int)EventStatus.InProgress });
        rescheduleCommand.Parameters.Add(new NpgsqlParameter("@scheduled_at", NpgsqlDbType.TimestampTz) { Value = now });
        rescheduleCommand.Parameters.Add(new NpgsqlParameter("@last_updated_at", NpgsqlDbType.TimestampTz) { Value = now });
        rescheduleCommand.Parameters.Add(new NpgsqlParameter("@max_processing_timeouts", NpgsqlDbType.Integer) { Value = _eventBrokerSettings.MaxProcessingTimeouts });
        rescheduleCommand.Parameters.Add(new NpgsqlParameter("@claimed_before", NpgsqlDbType.TimestampTz) { Value = claimedBefore });
        rescheduleCommand.Parameters.Add(new NpgsqlParameter("@dead_lettered", NpgsqlDbType.Integer) { Value = (int)EventStatus.DeadLettered });
        rescheduleCommand.Parameters.Add(new NpgsqlParameter("@error", NpgsqlDbType.Text) { Value = "Max processing timeouts count reached" });

        _ = await rescheduleCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeadLetterUnclaimedAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var scheduledBefore = now - _eventBrokerSettings.UnclaimedTtl;

        using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = _deadLetterUnclaimedSqlUpdate;
        command.Parameters.Add(new NpgsqlParameter("@dead_lettered", NpgsqlDbType.Integer) { Value = (int)EventStatus.DeadLettered });
        command.Parameters.Add(new NpgsqlParameter("@last_updated_at", NpgsqlDbType.TimestampTz) { Value = now });
        command.Parameters.Add(new NpgsqlParameter("@error", NpgsqlDbType.Text) { Value = "Unclaimed event" });
        command.Parameters.Add(new NpgsqlParameter("@scheduled", NpgsqlDbType.Integer) { Value = (int)EventStatus.Scheduled });
        command.Parameters.Add(new NpgsqlParameter("@scheduled_before", NpgsqlDbType.TimestampTz) { Value = scheduledBefore });

        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteCompletedAndDeadLetteredExceedingTtlAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var deadLetteredBefore = now - _eventBrokerSettings.DeadLetteredRecordTtl;
        var completedBefore = now - _eventBrokerSettings.CompletedRecordTtl;

        using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = _deleteDeadLetteredAndCompletedExceededTtlSql;
        command.Parameters.Add(new NpgsqlParameter("@completed", NpgsqlDbType.Integer) { Value = (int)EventStatus.Completed });
        command.Parameters.Add(new NpgsqlParameter("@dead_lettered", NpgsqlDbType.Integer) { Value = (int)EventStatus.DeadLettered });
        command.Parameters.Add(new NpgsqlParameter("@completed_before", NpgsqlDbType.TimestampTz) { Value = completedBefore });
        command.Parameters.Add(new NpgsqlParameter("@dead_lettered_before", NpgsqlDbType.TimestampTz) { Value = deadLetteredBefore });

        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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

        using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var insertCommand = connection.CreateCommand();
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

        _ = await insertCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
