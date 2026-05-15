using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using M.EventBrokerSlim.Persistent;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace M.EventBrokerSlim.PersistentEvents.Redis.Internal;

internal class RedisStorage : IEventStorage, IDisposable
{
    private readonly RedisSettings _redisSettings;
    private readonly PersistentEventBrokerSettings _eventBrokerSettings;
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly IDatabase _db;
    private readonly ILogger<RedisStorage> _logger;

    private readonly string _scheduledIndexKey;
    private readonly string _inProgressIndexKey;
    private readonly string _completedIndexKey;
    private readonly string _deadLetteredIndexKey;
    private readonly LuaScripts _luaScripts;

    public RedisStorage(RedisSettings redisSettings, PersistentEventBrokerSettings eventBrokerSettings, IConnectionMultiplexer connectionMultiplexer, ILogger<RedisStorage> logger)
    {
        _redisSettings = redisSettings;
        _eventBrokerSettings = eventBrokerSettings;
        _connectionMultiplexer = connectionMultiplexer;
        _logger = logger;

        _db = _connectionMultiplexer.GetDatabase();
        _scheduledIndexKey = $"{{{redisSettings.KeyPrefix}}}:idx:scheduled";
        _inProgressIndexKey = $"{{{redisSettings.KeyPrefix}}}:idx:in_progress";
        _completedIndexKey = $"{{{redisSettings.KeyPrefix}}}:idx:completed";
        _deadLetteredIndexKey = $"{{{redisSettings.KeyPrefix}}}:idx:dead_lettered";
        _luaScripts = new LuaScripts(connectionMultiplexer, logger);
        _luaScripts.Load();
        _connectionMultiplexer.ConnectionRestored += OnConnectionRestored;
    }

    public async Task CompleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var argv = new
        {
            recordKey = (RedisKey)id,
            inProgressIndexKey = (RedisKey)_inProgressIndexKey,
            completedIndexKey = (RedisKey)_completedIndexKey,
            completedStatus = (RedisValue)(int)EventStatus.Completed,
            newLastUpdatedAt = (RedisValue)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        _ = await _luaScripts.Complete.EvaluateAsync(_db, argv).ConfigureAwait(false);
    }

    public async Task DeadLetterAsync(string id, string? error = null, CancellationToken cancellationToken = default)
    {
        var argv = new
        {
            recordKey = (RedisKey)id,
            inProgressIndexKey = (RedisKey)_inProgressIndexKey,
            scheduledIndexKey = (RedisKey)_scheduledIndexKey,
            deadLetteredIndexKey = (RedisKey)_deadLetteredIndexKey,
            deadLetteredStatus = (RedisValue)(int)EventStatus.DeadLettered,
            lastUpdatedAt = (RedisValue)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            error = (RedisValue)(error ?? string.Empty)
        };

        _ = await _luaScripts.DeadLetter.EvaluateAsync(_db, argv).ConfigureAwait(false);
    }

    public async Task<IEnumerable<ScheduledEventRecord>> FetchScheduledAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var argv = new
        {
            scheduledIndexKey = (RedisKey)_scheduledIndexKey,
            now = (RedisValue)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            batchSize = (RedisValue)batchSize
        };

        RedisResult result = await _luaScripts.FetchScheduled.EvaluateAsync(_db, argv).ConfigureAwait(false);

        if(result.IsNull || result.Length == 0)
        {
            return Enumerable.Empty<ScheduledEventRecord>();
        }

        const int fieldsPerRecord = 4;
        var records = new List<ScheduledEventRecord>(result.Length / fieldsPerRecord);
        for(int i = 0; i < result.Length; i += fieldsPerRecord)
        {
            string id = (string)result[i]!;
            string eventName = (string)result[i + 1]!;
            string handlerName = (string)result[i + 2]!;
            DateTime utcDateTime = DateTimeOffset.FromUnixTimeMilliseconds((long)result[i + 3]).UtcDateTime;
            records.Add(new ScheduledEventRecord(id, utcDateTime, eventName, handlerName));
        }

        return records;
    }

    public async Task RetryAsync(string id, int attemptCount, TimeSpan delay, string? error = null, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var scheduledAt = now.Add(delay);
        var argv = new
        {
            recordKey = (RedisKey)id,
            scheduledIndexKey = (RedisKey)_scheduledIndexKey,
            inProgressIndexKey = (RedisKey)_inProgressIndexKey,
            scheduledStatus = (RedisValue)(int)EventStatus.Scheduled,
            scheduledAt = (RedisValue)scheduledAt.ToUnixTimeMilliseconds(),
            attemptCount = (RedisValue)attemptCount,
            retryLastDelay = (RedisValue)delay.TotalMilliseconds,
            lastUpdatedAt = (RedisValue)now.ToUnixTimeMilliseconds(),
            error = (RedisValue)(error ?? string.Empty)
        };

        _ = await _luaScripts.Retry.EvaluateAsync(_db, argv).ConfigureAwait(false);
    }

    public async Task<EventRecord> TryClaimAsync(ScheduledEventRecord scheduledEventRecord, EventRegistry eventRegistry, CancellationToken cancellationToken = default)
    {
        long nowUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var argv = new
        {
            recordKey = (RedisKey)scheduledEventRecord.Id,
            scheduledIndexKey = (RedisKey)_scheduledIndexKey,
            inProgressIndexKey = (RedisKey)_inProgressIndexKey,
            inProgressStatus = (RedisValue)(int)EventStatus.InProgress,
            scheduledStatus = (RedisValue)(int)EventStatus.Scheduled,
            claimedAt = (RedisValue)nowUnixMilliseconds,
            newLastUpdatedAt = (RedisValue)nowUnixMilliseconds,
            expectedLastUpdatedAt = (RedisValue)(new DateTimeOffset(scheduledEventRecord.LastUpdatedAt).ToUnixTimeMilliseconds())
        };

        RedisResult result = await _luaScripts.TryClaim.EvaluateAsync(_db, argv).ConfigureAwait(false);
        if(result == null || result.IsNull || result.Length == 0)
        {
            // nil returned from Lua (record missing, OCC failure, wrong status)
            return EventRecord.Empty;
        }

        Dictionary<string, RedisResult> record = result.ToDictionary();
        string eventId = GetString("event_id", record, scheduledEventRecord.Id);
        string eventName = GetString("event_name", record, scheduledEventRecord.Id);
        string handlerName = GetString("handler_name", record, scheduledEventRecord.Id);
        string stringPayload = GetString("payload", record, scheduledEventRecord.Id);
        EventStatus status = (EventStatus)GetInt("status", record, scheduledEventRecord.Id);
        DateTime scheduledAt = GetDateTime("scheduled_at", record, scheduledEventRecord.Id);
        int retryAttemptCount = GetInt("retry_attempt_count", record, scheduledEventRecord.Id);
        TimeSpan retryLastDelay = TimeSpan.FromMilliseconds(GetLong("retry_last_delay", record, scheduledEventRecord.Id));
        var claimedAtValue = GetLong("claimed_at", record, scheduledEventRecord.Id);
        DateTime? claimedAt = claimedAtValue == 0 ? null : DateTimeOffset.FromUnixTimeMilliseconds(claimedAtValue).UtcDateTime;
        DateTime createdAt = GetDateTime("created_at", record, scheduledEventRecord.Id);
        DateTime lastUpdatedAt = GetDateTime("last_updated_at", record, scheduledEventRecord.Id);
        string lastError = GetString("last_error", record, scheduledEventRecord.Id);
        int processingTimeoutsCount = GetInt("processing_timeouts_count", record, scheduledEventRecord.Id);

        object? deserializedEvent = EventSerializer.DeserializePayload(scheduledEventRecord.Id, stringPayload, eventName, eventRegistry, _logger);
        if(deserializedEvent is null)
        {
            return EventRecord.Empty;
        }

        return new EventRecord(
            scheduledEventRecord.Id,
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

    public async Task RescheduleClaimedExceedingProcessingTimeoutAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var nowUnixMilliseconds = now.ToUnixTimeMilliseconds();

        var claimedBefore = now.Subtract(_eventBrokerSettings.ProcessingTimeout);
        var claimedBeforeUnixMilliseconds = claimedBefore.ToUnixTimeMilliseconds();

        var argv = new
        {
            inProgressIndexKey = (RedisKey)_inProgressIndexKey,
            scheduledIndexKey = (RedisKey)_scheduledIndexKey,
            deadLetteredIndexKey = (RedisKey)_deadLetteredIndexKey,
            scheduledStatus = (RedisValue)(int)EventStatus.Scheduled,
            deadLetteredStatus = (RedisValue)(int)EventStatus.DeadLettered,
            scheduledAt = (RedisValue)nowUnixMilliseconds,
            lastUpdatedAt = (RedisValue)nowUnixMilliseconds,
            maxProcessingTimeoutsCount = (RedisValue)_eventBrokerSettings.MaxProcessingTimeouts,
            claimedBefore = (RedisValue)claimedBeforeUnixMilliseconds,
            error = (RedisValue)"Max processing timeouts count reached"
        };

        _ = await _luaScripts.RescheduleClaimedExceedingProcessingTimeout.EvaluateAsync(_db, argv).ConfigureAwait(false);
    }

    public async Task DeadLetterUnclaimedAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var scheduledBefore = now - _eventBrokerSettings.UnclaimedTtl;
        var argv = new
        {
            scheduledIndexKey = (RedisKey)_scheduledIndexKey,
            deadLetteredIndexKey = (RedisKey)_deadLetteredIndexKey,
            deadLetteredStatus = (RedisValue)(int)EventStatus.DeadLettered,
            lastUpdatedAt = (RedisValue)now.ToUnixTimeMilliseconds(),
            error = (RedisValue)"Unclaimed event",
            scheduledBefore = (RedisValue)scheduledBefore.ToUnixTimeMilliseconds()
        };

        _ = await _luaScripts.DeadLetterUnclaimed.EvaluateAsync(_db, argv).ConfigureAwait(false);
    }

    public async Task DeleteCompletedAndDeadLetteredExceedingTtlAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var deadLetteredBefore = now - _eventBrokerSettings.DeadLetteredRecordTtl;
        var deadLetteredBeforeUnixMilliseconds = deadLetteredBefore.ToUnixTimeMilliseconds();
        var completedBefore = now - _eventBrokerSettings.CompletedRecordTtl;
        var completedBeforeUnixMilliseconds = completedBefore.ToUnixTimeMilliseconds();
        var argv = new
        {
            completedIndexKey = (RedisKey)_completedIndexKey,
            deadLetteredIndexKey = (RedisKey)_deadLetteredIndexKey,
            completedBefore = (RedisValue)completedBeforeUnixMilliseconds,
            deadLetteredBefore = (RedisValue)deadLetteredBeforeUnixMilliseconds
        };

        _ = await _luaScripts.DeleteCompletedAndDeadLetteredExceedingTtl.EvaluateAsync(_db, argv).ConfigureAwait(false);
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
        var payload = (RedisValue)EventSerializer.SerializePayload(publishedEvent);
        var scheduledIndexKey = (RedisKey)_scheduledIndexKey;
        var scheduledAtMilliseconds = (RedisValue)scheduledAt.ToUnixTimeMilliseconds();
        var nowMilliseconds = (RedisValue)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string eventId = Guid.NewGuid().ToString();
        var tasks = handlerNames
            .Select(async handlerName =>
            {
                var argv = new
                {
                    recordKey = (RedisKey)GenerateId(),
                    scheduledIndexKey,
                    eventId,
                    eventName = (RedisValue)eventName,
                    handlerName = (RedisValue)handlerName,
                    payload,
                    scheduledAt = scheduledAtMilliseconds,
                    now = nowMilliseconds,
                };

                await _luaScripts.Schedule.EvaluateAsync(_db, argv);
            })
            .ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private string GenerateId()
    {
#if NET9_0_OR_GREATER
        var id = $"{{{_redisSettings.KeyPrefix}}}:evt:{Guid.CreateVersion7().ToString("N")}";
#else
        var id = $"{{{_redisSettings.KeyPrefix}}}:evt:{Guid.NewGuid().ToString("N")}";
#endif
        return id;
    }

    private void OnConnectionRestored(object? sender, ConnectionFailedEventArgs e)
    {
        _logger.LogInformation("Redis connection restored ({EndPoint}). Reloading Lua scripts.", e.EndPoint);
        try
        {
            _luaScripts.Load();
        }
        catch(Exception ex)
        {
            _logger.LogWarning(ex, "LoadScripts failed after reconnect; NOSCRIPT fallback active.");
        }
    }

    private static string GetString(string key, Dictionary<string, RedisResult> record, string recordId)
    {
        if(record.TryGetValue(key, out var value) && !value.IsNull)
        {
            return (string?)value
                ?? throw new InvalidOperationException($"Null value for {key} in Lua result for event record id {recordId}");
        }

        throw new InvalidOperationException($"Missing {key} in Lua result for event record id {recordId}");
    }

    private static int GetInt(string key, Dictionary<string, RedisResult> record, string recordId)
    {
        if(record.TryGetValue(key, out var value) && !value.IsNull)
        {
            return (int)value;
        }

        throw new InvalidOperationException($"Missing {key} in Lua result for event record id {recordId}");
    }

    private static long GetLong(string key, Dictionary<string, RedisResult> record, string recordId)
    {
        if(record.TryGetValue(key, out var value) && !value.IsNull)
        {
            return (long)value;
        }

        throw new InvalidOperationException($"Missing {key} in Lua result for event record id {recordId}");
    }

    private static DateTime GetDateTime(string key, Dictionary<string, RedisResult> record, string recordId)
    {
        if(record.TryGetValue(key, out var value) && !value.IsNull)
        {
            long unixTimeMilliseconds = (long?)value
                ?? throw new InvalidOperationException($"Null value for {key} in Lua result for event record id {recordId}");
            return DateTimeOffset.FromUnixTimeMilliseconds(unixTimeMilliseconds).UtcDateTime;
        }

        throw new InvalidOperationException($"Missing {key} in Lua result for event record id {recordId}");
    }

    public void Dispose()
    {
        _connectionMultiplexer.ConnectionRestored -= OnConnectionRestored;
        if(!_redisSettings.UseRegisteredMultiplexer)
        {
            _connectionMultiplexer.Close();
            _connectionMultiplexer.Dispose();
        }
    }
}
