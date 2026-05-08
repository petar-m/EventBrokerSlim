using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using M.EventBrokerSlim.Persistent;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace M.EventBrokerSlim.PersistentEvents.SqlServer.Internal;

internal class SqlServerStorage : IEventStorage
{
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
    private readonly DatabaseSettings _databaseSettings;
    private readonly PersistentEventBrokerSettings _eventBrokerSettings;
    private readonly ILogger<SqlServerStorage> _logger;

    public SqlServerStorage(DatabaseSettings databaseSettings, PersistentEventBrokerSettings eventBrokerSettings, ILogger<SqlServerStorage> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseSettings.ConnectionString);
        _databaseSettings = databaseSettings;
        _eventBrokerSettings = eventBrokerSettings;
        _logger = logger ?? NullLogger<SqlServerStorage>.Instance;
        _completeSingleSqlUpdate = $"""
            UPDATE {databaseSettings.Schema}.{databaseSettings.Table}
            SET status = @completed, last_updated_at = @last_updated_at
            WHERE id = @id;
            """;
        _deadLetterSingleSqlUpdate = $"""
            UPDATE {databaseSettings.Schema}.{databaseSettings.Table}
            SET status = @dead_lettered, last_updated_at = @last_updated_at, last_error = @error
            WHERE id = @id;
            """;
        _fetchScheduledSqlQuery = $"""
            SELECT TOP(@batch_size) id, last_updated_at, event_name, handler_name
            FROM {databaseSettings.Schema}.{databaseSettings.Table}
            WHERE status = @scheduled AND scheduled_at <= @now
            ORDER BY scheduled_at ASC;
            """;
        _retrySingleSqlUpdate = $"""
            UPDATE {databaseSettings.Schema}.{databaseSettings.Table}
            SET status = @scheduled, scheduled_at = @scheduled_at, retry_attempt_count = @attempt_count, retry_last_delay = @retry_last_delay, last_updated_at = @last_updated_at, last_error = @error
            WHERE id = @id;
            """;
        _tryClaimSingleSqlUpdate = $"""
            UPDATE {databaseSettings.Schema}.{databaseSettings.Table}
            SET status = @in_progress, claimed_at = @claimed_at, last_updated_at = @last_updated_at
            OUTPUT INSERTED.id, INSERTED.event_id, INSERTED.event_name, INSERTED.handler_name, INSERTED.payload, 
                   INSERTED.status, INSERTED.scheduled_at, INSERTED.retry_attempt_count, INSERTED.retry_last_delay, 
                   INSERTED.claimed_at, INSERTED.created_at, INSERTED.last_updated_at, INSERTED.last_error, INSERTED.processing_timeouts_count
            WHERE id = @candidate_id AND status = @scheduled AND last_updated_at = @candidate_last_updated_at
            """;
        _scheduleSqlInsert = $$"""
            INSERT INTO {{databaseSettings.Schema}}.{{databaseSettings.Table}} (event_id, event_name, handler_name, payload, status, scheduled_at, retry_attempt_count, retry_last_delay, created_at, last_updated_at, processing_timeouts_count)
            VALUES {0};
            """;
        _rescheduleClaimedExceedingProcessingTimeoutUpdateSql = $"""
           UPDATE {databaseSettings.Schema}.{databaseSettings.Table}
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
            UPDATE {databaseSettings.Schema}.{databaseSettings.Table} SET
                status = @dead_lettered,
                last_updated_at = @last_updated_at,
                last_error = @error
            WHERE status = @scheduled AND scheduled_at <= @scheduled_before;
            """;
        _deleteDeadLetteredAndCompletedExceededTtlSql = $"""
            DELETE FROM {databaseSettings.Schema}.{databaseSettings.Table}
            WHERE 
            (status = @completed AND last_updated_at <= @completed_before) OR 
            (status = @dead_lettered AND last_updated_at <= @dead_lettered_before);
            """;
    }

    public async Task CompleteAsync(string id, CancellationToken cancellationToken = default)
    {
        using var connection = new SqlConnection(_databaseSettings.ConnectionString);
        using var updateCommand = connection.CreateCommand();
        updateCommand.CommandText = _completeSingleSqlUpdate;
        var now = DateTimeOffset.UtcNow;
        updateCommand.Parameters.Add(new SqlParameter("@completed", SqlDbType.Int) { Value = (int)EventStatus.Completed });
        updateCommand.Parameters.Add(new SqlParameter("@last_updated_at", SqlDbType.DateTimeOffset) { Value = now });
        updateCommand.Parameters.Add(new SqlParameter("@id", SqlDbType.BigInt) { Value = long.Parse(id) });

        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        _ = await updateCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeadLetterAsync(string id, string? error = null, CancellationToken cancellationToken = default)
    {
        using var connection = new SqlConnection(_databaseSettings.ConnectionString);
        using var updateCommand = connection.CreateCommand();
        updateCommand.CommandText = _deadLetterSingleSqlUpdate;
        var now = DateTimeOffset.UtcNow;
        updateCommand.Parameters.Add(new SqlParameter("@dead_lettered", SqlDbType.Int) { Value = (int)EventStatus.DeadLettered });
        updateCommand.Parameters.Add(new SqlParameter("@last_updated_at", SqlDbType.DateTimeOffset) { Value = now });
        updateCommand.Parameters.Add(new SqlParameter("@error", SqlDbType.NVarChar, -1) { Value = error is null ? DBNull.Value : error });
        updateCommand.Parameters.Add(new SqlParameter("@id", SqlDbType.BigInt) { Value = long.Parse(id) });

        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        _ = await updateCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IEnumerable<ScheduledEventRecord>> FetchScheduledAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        using var connection = new SqlConnection(_databaseSettings.ConnectionString);
        using var selectCommand = connection.CreateCommand();
        selectCommand.CommandText = _fetchScheduledSqlQuery;
        selectCommand.Parameters.Add(new SqlParameter("@scheduled", SqlDbType.Int) { Value = (int)EventStatus.Scheduled });
        selectCommand.Parameters.Add(new SqlParameter("@now", SqlDbType.DateTimeOffset) { Value = DateTimeOffset.UtcNow });
        selectCommand.Parameters.Add(new SqlParameter("@batch_size", SqlDbType.Int) { Value = batchSize });
        
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        using SqlDataReader reader = await selectCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var events = new List<ScheduledEventRecord>(capacity: batchSize);
        while(await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            string eventRecordId = reader.GetInt64(0).ToString();
            DateTime lastUpdatedAt = reader.GetDateTimeOffset(1).UtcDateTime;
            string eventName = reader.GetString(2);
            string handlerName = reader.GetString(3);
            var eventRecord = new ScheduledEventRecord(eventRecordId, lastUpdatedAt, eventName, handlerName);
            events.Add(eventRecord);
        }

        return events;
    }

    public async Task RetryAsync(string id, int attemptCount, TimeSpan delay, string? error = null, CancellationToken cancellationToken = default)
    {
        using var connection = new SqlConnection(_databaseSettings.ConnectionString);
        using var updateCommand = connection.CreateCommand();
        updateCommand.CommandText = _retrySingleSqlUpdate;
        var now = DateTimeOffset.UtcNow;
        var scheduledAt = now.Add(delay);
        updateCommand.Parameters.Add(new SqlParameter("@scheduled", SqlDbType.Int) { Value = (int)EventStatus.Scheduled });
        updateCommand.Parameters.Add(new SqlParameter("@scheduled_at", SqlDbType.DateTimeOffset) { Value = scheduledAt });
        updateCommand.Parameters.Add(new SqlParameter("@attempt_count", SqlDbType.Int) { Value = attemptCount });
        updateCommand.Parameters.Add(new SqlParameter("@retry_last_delay", SqlDbType.BigInt) { Value = (long)delay.TotalMilliseconds });
        updateCommand.Parameters.Add(new SqlParameter("@last_updated_at", SqlDbType.DateTimeOffset) { Value = now });
        updateCommand.Parameters.Add(new SqlParameter("@error", SqlDbType.NVarChar, -1) { Value = error is null ? DBNull.Value : error });
        updateCommand.Parameters.Add(new SqlParameter("@id", SqlDbType.BigInt) { Value = long.Parse(id) });

        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        _ = await updateCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<EventRecord> TryClaimAsync(ScheduledEventRecord scheduledEventRecord, EventRegistry eventRegistry, CancellationToken cancellationToken = default)
    {
        using var connection = new SqlConnection(_databaseSettings.ConnectionString);
        using var updateCommand = connection.CreateCommand();
        updateCommand.CommandText = _tryClaimSingleSqlUpdate;
        updateCommand.Parameters.Add(new SqlParameter("@in_progress", SqlDbType.Int) { Value = (int)EventStatus.InProgress });
        var now = DateTimeOffset.UtcNow;
        updateCommand.Parameters.Add(new SqlParameter("@claimed_at", SqlDbType.DateTimeOffset) { Value = now });
        updateCommand.Parameters.Add(new SqlParameter("@last_updated_at", SqlDbType.DateTimeOffset) { Value = now });
        updateCommand.Parameters.Add(new SqlParameter("@candidate_id", SqlDbType.BigInt) { Value = long.Parse(scheduledEventRecord.Id) });
        updateCommand.Parameters.Add(new SqlParameter("@candidate_last_updated_at", SqlDbType.DateTimeOffset) { Value = scheduledEventRecord.LastUpdatedAt });
        updateCommand.Parameters.Add(new SqlParameter("@scheduled", SqlDbType.Int) { Value = (int)EventStatus.Scheduled });
        
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        using SqlDataReader reader = await updateCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        if(await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            string eventRecordId = reader.GetInt64(0).ToString();
            string eventId = reader.GetString(1);
            string eventName = reader.GetString(2);
            string handlerName = reader.GetString(3);
            string stringPayload = reader.GetString(4);
            EventStatus status = (EventStatus)reader.GetInt32(5);
            DateTime scheduledAt = reader.GetDateTimeOffset(6).UtcDateTime;
            int retryAttemptCount = reader.GetInt32(7);
            TimeSpan retryLastDelay = TimeSpan.FromMilliseconds(reader.GetInt64(8));
            DateTime? claimedAt = reader.IsDBNull(9) ? null : reader.GetDateTimeOffset(9).UtcDateTime;
            DateTime createdAt = reader.GetDateTimeOffset(10).UtcDateTime;
            DateTime lastUpdatedAt = reader.GetDateTimeOffset(11).UtcDateTime;
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

        using var connection = new SqlConnection(_databaseSettings.ConnectionString);

        using var rescheduleCommand = connection.CreateCommand();
        rescheduleCommand.CommandText = _rescheduleClaimedExceedingProcessingTimeoutUpdateSql;
        rescheduleCommand.Parameters.Add(new SqlParameter("@scheduled", SqlDbType.Int) { Value = (int)EventStatus.Scheduled });
        rescheduleCommand.Parameters.Add(new SqlParameter("@in_progress", SqlDbType.Int) { Value = (int)EventStatus.InProgress });
        rescheduleCommand.Parameters.Add(new SqlParameter("@scheduled_at", SqlDbType.DateTimeOffset) { Value = now });
        rescheduleCommand.Parameters.Add(new SqlParameter("@last_updated_at", SqlDbType.DateTimeOffset) { Value = now });
        rescheduleCommand.Parameters.Add(new SqlParameter("@max_processing_timeouts", SqlDbType.Int) { Value = _eventBrokerSettings.MaxProcessingTimeouts });
        rescheduleCommand.Parameters.Add(new SqlParameter("@claimed_before", SqlDbType.DateTimeOffset) { Value = claimedBefore });
        rescheduleCommand.Parameters.Add(new SqlParameter("@dead_lettered", SqlDbType.Int) { Value = (int)EventStatus.DeadLettered });
        rescheduleCommand.Parameters.Add(new SqlParameter("@error", SqlDbType.NVarChar, -1) { Value = "Max processing timeouts count reached" });

        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        _ = await rescheduleCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeadLetterUnclaimedAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var scheduledBefore = now - _eventBrokerSettings.UnclaimedTtl;

        using var connection = new SqlConnection(_databaseSettings.ConnectionString);
        using var command = connection.CreateCommand();
        command.CommandText = _deadLetterUnclaimedSqlUpdate;
        command.Parameters.Add(new SqlParameter("@dead_lettered", SqlDbType.Int) { Value = (int)EventStatus.DeadLettered });
        command.Parameters.Add(new SqlParameter("@last_updated_at", SqlDbType.DateTimeOffset) { Value = now });
        command.Parameters.Add(new SqlParameter("@error", SqlDbType.NVarChar, -1) { Value = "Unclaimed event" });
        command.Parameters.Add(new SqlParameter("@scheduled", SqlDbType.Int) { Value = (int)EventStatus.Scheduled });
        command.Parameters.Add(new SqlParameter("@scheduled_before", SqlDbType.DateTimeOffset) { Value = scheduledBefore });

        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteCompletedAndDeadLetteredExceedingTtlAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var deadLetteredBefore = now - _eventBrokerSettings.DeadLetteredRecordTtl;
        var completedBefore = now - _eventBrokerSettings.CompletedRecordTtl;

        using var connection = new SqlConnection(_databaseSettings.ConnectionString);
        using var command = connection.CreateCommand();
        command.CommandText = _deleteDeadLetteredAndCompletedExceededTtlSql;
        command.Parameters.Add(new SqlParameter("@completed", SqlDbType.Int) { Value = (int)EventStatus.Completed });
        command.Parameters.Add(new SqlParameter("@dead_lettered", SqlDbType.Int) { Value = (int)EventStatus.DeadLettered });
        command.Parameters.Add(new SqlParameter("@completed_before", SqlDbType.DateTimeOffset) { Value = completedBefore });
        command.Parameters.Add(new SqlParameter("@dead_lettered_before", SqlDbType.DateTimeOffset) { Value = deadLetteredBefore });
        
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
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

        using var connection = new SqlConnection(_databaseSettings.ConnectionString);
        using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText = command;
        insertCommand.Parameters.Add(new SqlParameter("@event_id", SqlDbType.NVarChar, 128) { Value = eventId });
        insertCommand.Parameters.Add(new SqlParameter("@event_name", SqlDbType.NVarChar, 128) { Value = eventName });
        insertCommand.Parameters.Add(new SqlParameter("@payload", SqlDbType.NVarChar, -1) { Value = payload });
        insertCommand.Parameters.Add(new SqlParameter("@status", SqlDbType.Int) { Value = (int)EventStatus.Scheduled });
        insertCommand.Parameters.Add(new SqlParameter("@scheduled_at", SqlDbType.DateTimeOffset) { Value = scheduledAt });
        insertCommand.Parameters.Add(new SqlParameter("@retry_attempt_count", SqlDbType.Int) { Value = 0 });
        insertCommand.Parameters.Add(new SqlParameter("@retry_last_delay", SqlDbType.BigInt) { Value = 0 });
        insertCommand.Parameters.Add(new SqlParameter("@created_at", SqlDbType.DateTimeOffset) { Value = now });
        insertCommand.Parameters.Add(new SqlParameter("@last_updated_at", SqlDbType.DateTimeOffset) { Value = now });
        insertCommand.Parameters.Add(new SqlParameter("@processing_timeouts_count", SqlDbType.Int) { Value = 0 });
        if(handlerNames.Length >= 1)
        {
            insertCommand.Parameters.Add(new SqlParameter("@handler_name_0", SqlDbType.NVarChar, 128) { Value = handlerNames[0] });
        }

        if(handlerNames.Length >= 2)
        {
            insertCommand.Parameters.Add(new SqlParameter("@handler_name_1", SqlDbType.NVarChar, 128) { Value = handlerNames[1] });
        }

        if(handlerNames.Length >= 3)
        {
            insertCommand.Parameters.Add(new SqlParameter("@handler_name_2", SqlDbType.NVarChar, 128) { Value = handlerNames[2] });
        }

        if(handlerNames.Length > 3)
        {
            for(int i = 3; i < handlerNames.Length; i++)
            {
                insertCommand.Parameters.Add(new SqlParameter($"@handler_name_{i}", SqlDbType.NVarChar, 128) { Value = handlerNames[i] });
            }
        }

        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        _ = await insertCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
