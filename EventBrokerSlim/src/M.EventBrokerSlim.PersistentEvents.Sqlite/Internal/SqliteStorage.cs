using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using M.EventBrokerSlim.Persistent;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace M.EventBrokerSlim.PersistentEvents.Sqlite.Internal;

internal class SqliteStorage : IEventStorage
{
    private readonly string _connectionString;
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
    private readonly ILogger<SqliteStorage> _logger;

    public SqliteStorage(DatabaseSettings databaseSettings, PersistentEventBrokerSettings eventBrokerSettings, ILogger<SqliteStorage> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseSettings.ConnectionString);
        _connectionString = databaseSettings.ConnectionString;
        _eventBrokerSettings = eventBrokerSettings;
        _logger = logger ?? NullLogger<SqliteStorage>.Instance;

        string table = databaseSettings.Table;

        _completeSingleSqlUpdate = $"""
            UPDATE {table}
            SET status = @completed, last_updated_at = @last_updated_at
            WHERE id = @id;
            """;
        _deadLetterSingleSqlUpdate = $"""
            UPDATE {table}
            SET status = @dead_lettered, last_updated_at = @last_updated_at, last_error = @error
            WHERE id = @id;
            """;
        _fetchScheduledSqlQuery = $"""
            SELECT id, last_updated_at, event_name, handler_name
            FROM {table}
            WHERE status = @scheduled AND scheduled_at <= @now
            ORDER BY scheduled_at ASC
            LIMIT @batch_size;
            """;
        _retrySingleSqlUpdate = $"""
            UPDATE {table}
            SET status = @scheduled, scheduled_at = @scheduled_at, retry_attempt_count = @attempt_count, retry_last_delay = @retry_last_delay, last_updated_at = @last_updated_at, last_error = @error
            WHERE id = @id;
            """;
        _tryClaimSingleSqlUpdate = $"""
            UPDATE {table}
            SET status = @in_progress, claimed_at = @claimed_at, last_updated_at = @last_updated_at
            WHERE id = @candidate_id AND status = @scheduled AND last_updated_at = @candidate_last_updated_at
            RETURNING id, event_id, event_name, handler_name, payload, status, scheduled_at, retry_attempt_count, retry_last_delay, claimed_at, created_at, last_updated_at, last_error, processing_timeouts_count;
            """;
        _scheduleSqlInsert = $$"""
            INSERT INTO {{table}} (event_id, event_name, handler_name, payload, status, scheduled_at, retry_attempt_count, retry_last_delay, created_at, last_updated_at, processing_timeouts_count)
            VALUES {0};
            """;
        _rescheduleClaimedExceedingProcessingTimeoutUpdateSql = $"""
           UPDATE {table}
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
            UPDATE {table} SET
                status = @dead_lettered,
                last_updated_at = @last_updated_at,
                last_error = @error
            WHERE status = @scheduled AND scheduled_at <= @scheduled_before;
            """;
        _deleteDeadLetteredAndCompletedExceededTtlSql = $"""
            DELETE FROM {table}
            WHERE
            (status = @completed AND last_updated_at <= @completed_before) OR
            (status = @dead_lettered AND last_updated_at <= @dead_lettered_before);
            """;
    }

    public async Task CompleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var updateCommand = connection.CreateCommand();
        updateCommand.CommandText = _completeSingleSqlUpdate;
        var now = DateTime.UtcNow;
        updateCommand.Parameters.AddWithValue("@completed", (int)EventStatus.Completed);
        updateCommand.Parameters.AddWithValue("@last_updated_at", FormatDateTime(now));
        updateCommand.Parameters.AddWithValue("@id", long.Parse(id));

        _ = await updateCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeadLetterAsync(string id, string? error = null, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var updateCommand = connection.CreateCommand();
        updateCommand.CommandText = _deadLetterSingleSqlUpdate;
        var now = DateTime.UtcNow;
        updateCommand.Parameters.AddWithValue("@dead_lettered", (int)EventStatus.DeadLettered);
        updateCommand.Parameters.AddWithValue("@last_updated_at", FormatDateTime(now));
        updateCommand.Parameters.AddWithValue("@error", error is null ? DBNull.Value : (object)error);
        updateCommand.Parameters.AddWithValue("@id", long.Parse(id));

        _ = await updateCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IEnumerable<ScheduledEventRecord>> FetchScheduledAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var selectCommand = connection.CreateCommand();
        selectCommand.CommandText = _fetchScheduledSqlQuery;
        selectCommand.Parameters.AddWithValue("@scheduled", (int)EventStatus.Scheduled);
        selectCommand.Parameters.AddWithValue("@now", FormatDateTime(DateTime.UtcNow));
        selectCommand.Parameters.AddWithValue("@batch_size", batchSize);

        await using SqliteDataReader reader = await selectCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var events = new List<ScheduledEventRecord>(capacity: batchSize);
        while(await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            string eventRecordId = reader.GetInt64(0).ToString();
            DateTime lastUpdatedAt = ParseDateTime(reader.GetString(1));
            string eventName = reader.GetString(2);
            string handlerName = reader.GetString(3);
            events.Add(new ScheduledEventRecord(eventRecordId, lastUpdatedAt, eventName, handlerName));
        }

        return events;
    }

    public async Task RetryAsync(string id, int attemptCount, TimeSpan delay, string? error = null, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var updateCommand = connection.CreateCommand();
        updateCommand.CommandText = _retrySingleSqlUpdate;
        var now = DateTime.UtcNow;
        var scheduledAt = now.Add(delay);
        updateCommand.Parameters.AddWithValue("@scheduled", (int)EventStatus.Scheduled);
        updateCommand.Parameters.AddWithValue("@scheduled_at", FormatDateTime(scheduledAt));
        updateCommand.Parameters.AddWithValue("@attempt_count", attemptCount);
        updateCommand.Parameters.AddWithValue("@retry_last_delay", (long)delay.TotalMilliseconds);
        updateCommand.Parameters.AddWithValue("@last_updated_at", FormatDateTime(now));
        updateCommand.Parameters.AddWithValue("@error", error is null ? DBNull.Value : (object)error);
        updateCommand.Parameters.AddWithValue("@id", long.Parse(id));

        _ = await updateCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<EventRecord> TryClaimAsync(ScheduledEventRecord scheduledEventRecord, EventRegistry eventRegistry, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var updateCommand = connection.CreateCommand();
        updateCommand.CommandText = _tryClaimSingleSqlUpdate;
        var now = DateTime.UtcNow;
        updateCommand.Parameters.AddWithValue("@in_progress", (int)EventStatus.InProgress);
        updateCommand.Parameters.AddWithValue("@claimed_at", FormatDateTime(now));
        updateCommand.Parameters.AddWithValue("@last_updated_at", FormatDateTime(now));
        updateCommand.Parameters.AddWithValue("@candidate_id", long.Parse(scheduledEventRecord.Id));
        updateCommand.Parameters.AddWithValue("@candidate_last_updated_at", FormatDateTime(scheduledEventRecord.LastUpdatedAt));
        updateCommand.Parameters.AddWithValue("@scheduled", (int)EventStatus.Scheduled);

        await using SqliteDataReader reader = await updateCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        if(await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            string eventRecordId = reader.GetInt64(0).ToString();
            string eventId = reader.GetString(1);
            string eventName = reader.GetString(2);
            string handlerName = reader.GetString(3);
            string stringPayload = reader.GetString(4);
            EventStatus status = (EventStatus)reader.GetInt32(5);
            DateTime scheduledAt = ParseDateTime(reader.GetString(6));
            int retryAttemptCount = reader.GetInt32(7);
            TimeSpan retryLastDelay = TimeSpan.FromMilliseconds(reader.GetInt64(8));
            DateTime? claimedAt = reader.IsDBNull(9) ? null : ParseDateTime(reader.GetString(9));
            DateTime createdAt = ParseDateTime(reader.GetString(10));
            DateTime lastUpdatedAt = ParseDateTime(reader.GetString(11));
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
        var now = DateTime.UtcNow;
        var claimedBefore = now.Subtract(_eventBrokerSettings.ProcessingTimeout);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var rescheduleCommand = connection.CreateCommand();
        rescheduleCommand.CommandText = _rescheduleClaimedExceedingProcessingTimeoutUpdateSql;
        rescheduleCommand.Parameters.AddWithValue("@scheduled", (int)EventStatus.Scheduled);
        rescheduleCommand.Parameters.AddWithValue("@in_progress", (int)EventStatus.InProgress);
        rescheduleCommand.Parameters.AddWithValue("@scheduled_at", FormatDateTime(now));
        rescheduleCommand.Parameters.AddWithValue("@last_updated_at", FormatDateTime(now));
        rescheduleCommand.Parameters.AddWithValue("@max_processing_timeouts", _eventBrokerSettings.MaxProcessingTimeouts);
        rescheduleCommand.Parameters.AddWithValue("@claimed_before", FormatDateTime(claimedBefore));
        rescheduleCommand.Parameters.AddWithValue("@dead_lettered", (int)EventStatus.DeadLettered);
        rescheduleCommand.Parameters.AddWithValue("@error", "Max processing timeouts count reached");

        _ = await rescheduleCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeadLetterUnclaimedAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var scheduledBefore = now.Subtract(_eventBrokerSettings.UnclaimedTtl);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = _deadLetterUnclaimedSqlUpdate;
        command.Parameters.AddWithValue("@dead_lettered", (int)EventStatus.DeadLettered);
        command.Parameters.AddWithValue("@last_updated_at", FormatDateTime(now));
        command.Parameters.AddWithValue("@error", "Unclaimed event");
        command.Parameters.AddWithValue("@scheduled", (int)EventStatus.Scheduled);
        command.Parameters.AddWithValue("@scheduled_before", FormatDateTime(scheduledBefore));

        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteCompletedAndDeadLetteredExceedingTtlAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var deadLetteredBefore = now.Subtract(_eventBrokerSettings.DeadLetteredRecordTtl);
        var completedBefore = now.Subtract(_eventBrokerSettings.CompletedRecordTtl);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = _deleteDeadLetteredAndCompletedExceededTtlSql;
        command.Parameters.AddWithValue("@completed", (int)EventStatus.Completed);
        command.Parameters.AddWithValue("@dead_lettered", (int)EventStatus.DeadLettered);
        command.Parameters.AddWithValue("@completed_before", FormatDateTime(completedBefore));
        command.Parameters.AddWithValue("@dead_lettered_before", FormatDateTime(deadLetteredBefore));

        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ScheduleAsync<TEvent>(TEvent publishedEvent, string eventName, ImmutableArray<string> handlerNames, CancellationToken cancellationToken = default)
    {
        await WriteAsync(publishedEvent, eventName, handlerNames, DateTime.UtcNow, cancellationToken).ConfigureAwait(false);
    }

    public async Task ScheduleDeferredAsync<TEvent>(TEvent publishedEvent, TimeSpan deferDuration, string eventName, ImmutableArray<string> handlerNames, CancellationToken cancellationToken = default)
    {
        DateTime scheduledAt = DateTime.UtcNow.Add(deferDuration);
        await WriteAsync(publishedEvent, eventName, handlerNames, scheduledAt, cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteAsync<TEvent>(TEvent publishedEvent, string eventName, ImmutableArray<string> handlerNames, DateTime scheduledAt, CancellationToken cancellationToken)
    {
        var commandText = _insertSqlCache.GetOrAdd(
            handlerNames.Length,
            length =>
            {
                var insertValuesBuilder = new StringBuilder();
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
        var now = DateTime.UtcNow;

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText = commandText;
        insertCommand.Parameters.AddWithValue("@event_id", eventId);
        insertCommand.Parameters.AddWithValue("@event_name", eventName);
        insertCommand.Parameters.AddWithValue("@payload", payload);
        insertCommand.Parameters.AddWithValue("@status", (int)EventStatus.Scheduled);
        insertCommand.Parameters.AddWithValue("@scheduled_at", FormatDateTime(scheduledAt));
        insertCommand.Parameters.AddWithValue("@retry_attempt_count", 0);
        insertCommand.Parameters.AddWithValue("@retry_last_delay", 0L);
        insertCommand.Parameters.AddWithValue("@created_at", FormatDateTime(now));
        insertCommand.Parameters.AddWithValue("@last_updated_at", FormatDateTime(now));
        insertCommand.Parameters.AddWithValue("@processing_timeouts_count", 0);

        if(handlerNames.Length >= 1)
        {
            insertCommand.Parameters.AddWithValue("@handler_name_0", handlerNames[0]);
        }

        if(handlerNames.Length >= 2)
        {
            insertCommand.Parameters.AddWithValue("@handler_name_1", handlerNames[1]);
        }

        if(handlerNames.Length >= 3)
        {
            insertCommand.Parameters.AddWithValue("@handler_name_2", handlerNames[2]);
        }

        if(handlerNames.Length > 3)
        {
            for(int i = 3; i < handlerNames.Length; i++)
            {
                insertCommand.Parameters.AddWithValue($"@handler_name_{i}", handlerNames[i]);
            }
        }

        _ = await insertCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var pragmaCmd = connection.CreateCommand();
        pragmaCmd.CommandText = "PRAGMA busy_timeout=5000;";
        await pragmaCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private static string FormatDateTime(DateTime dt) => dt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTime ParseDateTime(string s) => DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
