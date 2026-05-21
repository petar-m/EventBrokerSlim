using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using LiteDB;
using M.EventBrokerSlim.Persistent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace M.EventBrokerSlim.PersistentEvents.LiteDb.Internal;

internal class LiteDbStorage : IEventStorage
{
    private readonly LiteDatabase _db;
    private readonly ILiteCollection<EventDocument> _collection;
    private readonly PersistentEventBrokerSettings _settings;
    private readonly ILogger<LiteDbStorage> _logger;

    public LiteDbStorage(LiteDbInstanceWrapper liteDbInstanceWrapper, DatabaseSettings databaseSettings, PersistentEventBrokerSettings settings, ILogger<LiteDbStorage> logger)
    {
        _db = liteDbInstanceWrapper.LiteDb;
        _collection = _db.GetCollection<EventDocument>(databaseSettings.Collection);
        _collection.EnsureIndex(x => x.Status);
        _collection.EnsureIndex(x => x.ScheduledAt);
        _collection.EnsureIndex(x => x.ClaimedAt);
        _collection.EnsureIndex(x => x.LastUpdatedAt);
        _settings = settings;
        _logger = logger ?? NullLogger<LiteDbStorage>.Instance;
    }

    public Task ScheduleAsync<TEvent>(TEvent publishedEvent, string eventName, ImmutableArray<string> handlerNames, CancellationToken cancellationToken = default)
    {
        WriteDocuments(publishedEvent, eventName, handlerNames, DateTime.UtcNow);
        return Task.CompletedTask;
    }

    public Task ScheduleDeferredAsync<TEvent>(TEvent publishedEvent, TimeSpan deferDuration, string eventName, ImmutableArray<string> handlerNames, CancellationToken cancellationToken = default)
    {
        WriteDocuments(publishedEvent, eventName, handlerNames, DateTime.UtcNow.Add(deferDuration));
        return Task.CompletedTask;
    }

    public Task<IEnumerable<ScheduledEventRecord>> FetchScheduledAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var projected = _collection.Query()
            .Where(x => x.Status == (int)EventStatus.Scheduled && x.ScheduledAt <= now)
            .OrderBy(x => x.ScheduledAt)
            .Select(x => new ScheduledProjection
            {
                Id = x.Id,
                LastUpdatedAt = x.LastUpdatedAt,
                EventName = x.EventName,
                HandlerName = x.HandlerName
            })
            .Limit(batchSize)
            .ToEnumerable();

        var records = new Lazy<List<ScheduledEventRecord>>(() => new List<ScheduledEventRecord>(batchSize), LazyThreadSafetyMode.None);
        foreach(ScheduledProjection item in projected)
        {
            records.Value.Add(new ScheduledEventRecord(item.Id.ToString(), AsUtc(item.LastUpdatedAt), item.EventName, item.HandlerName));
        }

        return Task.FromResult<IEnumerable<ScheduledEventRecord>>(records.IsValueCreated 
            ? records.Value 
            : Array.Empty<ScheduledEventRecord>());
    }

    public Task<EventRecord> TryClaimAsync(ScheduledEventRecord scheduledEventRecord, EventRegistry eventRegistry, CancellationToken cancellationToken = default)
    {
        var id = long.Parse(scheduledEventRecord.Id);
        var now = DateTime.UtcNow;
        var expectedLastUpdatedAt = scheduledEventRecord.LastUpdatedAt;

        var updated = _collection.UpdateMany(
            x => new EventDocument
            {
                Status = (int)EventStatus.InProgress,
                ClaimedAt = now,
                LastUpdatedAt = now
            },
            x => x.Id == id && x.Status == (int)EventStatus.Scheduled && x.LastUpdatedAt == expectedLastUpdatedAt);

        if(updated == 0)
        {
            return Task.FromResult(EventRecord.Empty);
        }

        var doc = _collection.FindById(id);
        if(doc is null)
        {
            return Task.FromResult(EventRecord.Empty);
        }

        object? deserializedEvent = EventSerializer.DeserializePayload(
            scheduledEventRecord.Id, doc.Payload, doc.EventName, eventRegistry, _logger);

        if(deserializedEvent is null)
        {
            return Task.FromResult(EventRecord.Empty);
        }

        return Task.FromResult(new EventRecord(
            scheduledEventRecord.Id,
            doc.EventId,
            doc.EventName,
            doc.HandlerName,
            doc.Payload,
            (EventStatus)doc.Status,
            AsUtc(doc.ScheduledAt),
            doc.RetryAttemptCount,
            TimeSpan.FromMilliseconds(doc.RetryLastDelay),
            doc.ClaimedAt.HasValue ? AsUtc(doc.ClaimedAt.Value) : null,
            AsUtc(doc.CreatedAt),
            AsUtc(doc.LastUpdatedAt),
            deserializedEvent,
            doc.LastError,
            doc.ProcessingTimeoutsCount));
    }

    public Task CompleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var docId = long.Parse(id);
        var now = DateTime.UtcNow;
        _collection.UpdateMany(
            x => new EventDocument
            {
                Status = (int)EventStatus.Completed,
                LastUpdatedAt = now
            },
            x => x.Id == docId);

        return Task.CompletedTask;
    }

    public Task RetryAsync(string id, int attemptCount, TimeSpan delay, string? error = null, CancellationToken cancellationToken = default)
    {
        var docId = long.Parse(id);
        var now = DateTime.UtcNow;
        var scheduledAt = now.Add(delay);
        var retryLastDelay = (long)delay.TotalMilliseconds;
        _collection.UpdateMany(
            x => new EventDocument
            {
                Status = (int)EventStatus.Scheduled,
                ScheduledAt = scheduledAt,
                RetryAttemptCount = attemptCount,
                RetryLastDelay = retryLastDelay,
                LastUpdatedAt = now,
                LastError = error
            },
            x => x.Id == docId);

        return Task.CompletedTask;
    }

    public Task DeadLetterAsync(string id, string? error = null, CancellationToken cancellationToken = default)
    {
        var docId = long.Parse(id);
        var now = DateTime.UtcNow;
        _collection.UpdateMany(
            x => new EventDocument
            {
                Status = (int)EventStatus.DeadLettered,
                LastUpdatedAt = now,
                LastError = error
            },
            x => x.Id == docId);

        return Task.CompletedTask;
    }

    public Task RescheduleClaimedExceedingProcessingTimeoutAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var claimedBefore = now.Subtract(_settings.ProcessingTimeout);
        var maxProcessingTimeouts = _settings.MaxProcessingTimeouts;

        _db.BeginTrans();
        try
        {
            _collection.UpdateMany(
                x => new EventDocument
                {
                    Status = (int)EventStatus.DeadLettered,
                    LastUpdatedAt = now,
                    LastError = "Max processing timeouts count reached"
                },
                x => x.Status == (int)EventStatus.InProgress
                     && x.ClaimedAt <= claimedBefore
                     && x.ProcessingTimeoutsCount >= maxProcessingTimeouts);

            _collection.UpdateMany(
                x => new EventDocument
                {
                    Status = (int)EventStatus.Scheduled,
                    ScheduledAt = now,
                    ClaimedAt = null,
                    LastUpdatedAt = now,
                    ProcessingTimeoutsCount = x.ProcessingTimeoutsCount + 1
                },
                x => x.Status == (int)EventStatus.InProgress
                     && x.ClaimedAt <= claimedBefore);

            _db.Commit();
        }
        catch
        {
            _db.Rollback();
            throw;
        }

        return Task.CompletedTask;
    }

    public Task DeadLetterUnclaimedAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var scheduledBefore = now.Subtract(_settings.UnclaimedTtl);

        _collection.UpdateMany(
            x => new EventDocument
            {
                Status = (int)EventStatus.DeadLettered,
                LastUpdatedAt = now,
                LastError = "Unclaimed event"
            },
            x => x.Status == (int)EventStatus.Scheduled && x.ScheduledAt <= scheduledBefore);

        return Task.CompletedTask;
    }

    public Task DeleteCompletedAndDeadLetteredExceedingTtlAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var completedBefore = now.Subtract(_settings.CompletedRecordTtl);
        var deadLetteredBefore = now.Subtract(_settings.DeadLetteredRecordTtl);

        _collection.DeleteMany(x => x.Status == (int)EventStatus.Completed && x.LastUpdatedAt <= completedBefore);
        _collection.DeleteMany(x => x.Status == (int)EventStatus.DeadLettered && x.LastUpdatedAt <= deadLetteredBefore);

        return Task.CompletedTask;
    }

    private void WriteDocuments<TEvent>(TEvent publishedEvent, string eventName, ImmutableArray<string> handlerNames, DateTime scheduledAt)
    {
        string payload = EventSerializer.SerializePayload(publishedEvent);
        string eventId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;

        var documents = new List<EventDocument>(handlerNames.Length);
        foreach(var handlerName in handlerNames)
        {
            documents.Add(new EventDocument
            {
                EventId = eventId,
                EventName = eventName,
                HandlerName = handlerName,
                Payload = payload,
                Status = (int)EventStatus.Scheduled,
                ScheduledAt = scheduledAt,
                RetryAttemptCount = 0,
                RetryLastDelay = 0,
                CreatedAt = now,
                LastUpdatedAt = now,
                ProcessingTimeoutsCount = 0
            });
        }

        _collection.InsertBulk(documents);
    }

    private static DateTime AsUtc(DateTime dt) => dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
}
