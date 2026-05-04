using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace M.EventBrokerSlim.PersistentEvents.Redis.Internal;

internal sealed class LuaScripts
{
    private static readonly LuaScript _preparedSchedule = LuaScript.Prepare(
        """
        redis.call('HSET', @recordKey,
            'event_id',                  @eventId,
            'event_name',                @eventName,
            'handler_name',              @handlerName,
            'payload',                   @payload,
            'status',                    1,
            'scheduled_at',              @scheduledAt,
            'retry_attempt_count',       0,
            'retry_last_delay',          0,
            'claimed_at',                0,
            'created_at',                @now,
            'last_updated_at',           @now,
            'last_error',                '',
            'processing_timeouts_count', 0)

        redis.call('ZADD', @scheduledIndexKey, @scheduledAt, @recordKey)
        """);

    private static readonly LuaScript _preparedTryClaim = LuaScript.Prepare(
        """
        local currentStatus = redis.call('HGET', @recordKey, 'status')
        if currentStatus == false then
            return nil
        end

        if currentStatus ~= @scheduledStatus then
            return nil  -- not Scheduled (already claimed/completed)
        end

        local currentLastUpdated = redis.call('HGET', @recordKey, 'last_updated_at')
        if tonumber(currentLastUpdated) ~= tonumber(@expectedLastUpdatedAt) then
            return nil  -- OCC failure: record modified since fetch
        end

        redis.call('HSET', @recordKey,
            'status',          @inProgressStatus,
            'claimed_at',      @claimedAt,
            'last_updated_at', @newLastUpdatedAt)

        redis.call('ZREM', @scheduledIndexKey, @recordKey)
        redis.call('ZADD', @inProgressIndexKey, @claimedAt, @recordKey)

        return redis.call('HGETALL', @recordKey)
        """
        );

    private static readonly LuaScript _preparedComplete = LuaScript.Prepare(
        """
        redis.call('HSET', @recordKey,
            'status',          @completedStatus,
            'last_updated_at', @newLastUpdatedAt)

        redis.call('ZREM', @inProgressIndexKey, @recordKey)
        redis.call('ZADD', @completedIndexKey,  @newLastUpdatedAt, @recordKey)
        """
        );

    private static readonly LuaScript _preparedRetry = LuaScript.Prepare(
        """
        redis.call('HSET', @recordKey,
            'status',              @scheduledStatus,
            'scheduled_at',        @scheduledAt,
            'retry_attempt_count', @attemptCount,
            'retry_last_delay',    @retryLastDelay,
            'last_updated_at',     @lastUpdatedAt,
            'last_error',          @error)

        redis.call('ZREM', @inProgressIndexKey, @recordKey)
        redis.call('ZADD', @scheduledIndexKey,  @scheduledAt, @recordKey)
        """
        );

    private static readonly LuaScript _preparedDeadLetter = LuaScript.Prepare(
        """
        redis.call('HSET', @recordKey, 
            'status',          @deadLetteredStatus, 
            'last_updated_at', @lastUpdatedAt, 
            'last_error',      @error)

        -- Remove from whichever index it's currently in
        redis.call('ZREM', @inProgressIndexKey,   @recordKey)
        redis.call('ZREM', @scheduledIndexKey,    @recordKey)
        redis.call('ZADD', @deadLetteredIndexKey, @lastUpdatedAt, @recordKey)
        """
        );

    private static readonly LuaScript _preparedRescheduleClaimedExceedingProcessingTimeout = LuaScript.Prepare(
        """
        local ids = redis.call('ZRANGEBYSCORE', @inProgressIndexKey, '-inf', @claimedBefore)

        for _, recordKey in ipairs(ids) do
            local timeouts = tonumber(redis.call('HGET', recordKey, 'processing_timeouts_count'))

            if timeouts >= tonumber(@maxProcessingTimeoutsCount) then
                redis.call('HSET', recordKey,
                    'status',          @deadLetteredStatus,
                    'last_updated_at', @lastUpdatedAt,
                    'last_error',      @error)

                redis.call('ZREM', @inProgressIndexKey, recordKey)
                redis.call('ZADD', @deadLetteredIndexKey, @lastUpdatedAt, recordKey)
            else
                redis.call('HSET', recordKey,
                    'status',                    @scheduledStatus,
                    'scheduled_at',              @scheduledAt,
                    'last_updated_at',           @lastUpdatedAt,
                    'claimed_at',                0,
                    'processing_timeouts_count', timeouts + 1)

                redis.call('ZREM', @inProgressIndexKey, recordKey)
                redis.call('ZADD', @scheduledIndexKey,  @scheduledAt, recordKey)
            end
        end
        """
        );

    private static readonly LuaScript _preparedDeadLetterUnclaimed = LuaScript.Prepare(
    """
        local ids = redis.call('ZRANGEBYSCORE', @scheduledIndexKey, '-inf', @scheduledBefore)

        for _, recordKey in ipairs(ids) do
            redis.call('HSET', recordKey,
                'status',          @deadLetteredStatus,
                'last_updated_at', @lastUpdatedAt,
                'last_error',      @error)

            redis.call('ZREM', @scheduledIndexKey, recordKey)
            redis.call('ZADD', @deadLetteredIndexKey, @lastUpdatedAt, recordKey)
        end
        """
    );

    private static readonly LuaScript _preparedDeleteCompletedAndDeadLetteredExceedingTtlAsync = LuaScript.Prepare(
        """
        local completedIds     = redis.call('ZRANGEBYSCORE', @completedIndexKey,     '-inf', @completedBefore)
        local deadLetteredIds  = redis.call('ZRANGEBYSCORE', @deadLetteredIndexKey,  '-inf', @deadLetteredBefore)

        for _, recordKey in ipairs(completedIds) do
            redis.call('DEL',  recordKey)
            redis.call('ZREM', @completedIndexKey, recordKey)
        end

        for _, recordKey in ipairs(deadLetteredIds) do
            redis.call('DEL',  recordKey)
            redis.call('ZREM', @deadLetteredIndexKey, recordKey)
        end
        """
        );

#if NET9_0_OR_GREATER
    private readonly Lock _scriptLoadLock = new();
#else
    private readonly object _scriptLoadLock = new();
#endif

    private readonly IConnectionMultiplexer _multiplexer;
    private readonly ILogger _logger;

    public LuaScripts(IConnectionMultiplexer multiplexer, ILogger logger)
    {
        _multiplexer = multiplexer;
        _logger = logger;
    }

    public LoadedLuaScript Schedule { get; private set; } = null!;
    public LoadedLuaScript TryClaim { get; private set; } = null!;
    public LoadedLuaScript Complete { get; private set; } = null!;
    public LoadedLuaScript Retry { get; private set; } = null!;
    public LoadedLuaScript DeadLetter { get; private set; } = null!;
    public LoadedLuaScript RescheduleClaimedExceedingProcessingTimeout { get; private set; } = null!;
    public LoadedLuaScript DeadLetterUnclaimed { get; private set; } = null!;
    public LoadedLuaScript DeleteCompletedAndDeadLetteredExceedingTtl { get; private set; } = null!;

    public void Load()
    {
        // Lock prevents two ConnectionRestored events racing each other.
        // In the normal startup path there is no contention.
        lock(_scriptLoadLock)
        {
            var primaries = _multiplexer
                .GetEndPoints()
                .Select(endpoint => _multiplexer.GetServer(endpoint))
                .Where(server => server.IsConnected && !server.IsReplica)
                .ToList();

            if(primaries.Count == 0)
            {
                // Should not happen at construction time (Connect() would have thrown),
                // but can happen if ConnectionRestored fires before the shard is fully
                // back. Log and bail — the next ConnectionRestored will retry.
                _logger.LogWarning("LoadScripts: no connected primaries found; skipping.");
                return;
            }

            foreach(var server in primaries)
            {
                // .Load() issues SCRIPT LOAD synchronously and caches the SHA.
                // Subsequent executions use EVALSHA; StackExchange.Redis automatically
                // falls back to EVAL on NOSCRIPT, so a missed server is self-healing.
                Schedule = _preparedSchedule.Load(server);
                TryClaim = _preparedTryClaim.Load(server);
                Complete = _preparedComplete.Load(server);
                Retry = _preparedRetry.Load(server);
                DeadLetter = _preparedDeadLetter.Load(server);
                RescheduleClaimedExceedingProcessingTimeout = _preparedRescheduleClaimedExceedingProcessingTimeout.Load(server);
                DeadLetterUnclaimed = _preparedDeadLetterUnclaimed.Load(server);
                DeleteCompletedAndDeadLetteredExceedingTtl = _preparedDeleteCompletedAndDeadLetteredExceedingTtlAsync.Load(server);
            }

            _logger.LogDebug("LoadScripts: loaded onto {Count} primary(s).", primaries.Count);
        }
    }
}
