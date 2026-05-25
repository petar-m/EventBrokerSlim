using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using M.EventBrokerSlim.Persistent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace M.EventBrokerSlim.PersistentEvents.MongoDb.Internal;

internal class MongoDbStorage : IEventStorage
{
    private readonly IMongoCollection<EventDocument> _collection;
    private readonly PersistentEventBrokerSettings _settings;
    private readonly ILogger<MongoDbStorage> _logger;

    public MongoDbStorage(MongoClientWrapper clientWrapper, DatabaseSettings databaseSettings, PersistentEventBrokerSettings settings, ILogger<MongoDbStorage> logger)
    {
        _collection = clientWrapper.Database.GetCollection<EventDocument>(databaseSettings.CollectionName);
        _settings = settings;
        _logger = logger ?? NullLogger<MongoDbStorage>.Instance;
        EnsureIndexes();
    }

    public async Task ScheduleAsync<TEvent>(TEvent publishedEvent, string eventName, ImmutableArray<string> handlerNames, CancellationToken cancellationToken = default)
    {
        await InsertDocumentsAsync(publishedEvent, eventName, handlerNames, DateTime.UtcNow, cancellationToken).ConfigureAwait(false);
    }

    public async Task ScheduleDeferredAsync<TEvent>(TEvent publishedEvent, TimeSpan deferDuration, string eventName, ImmutableArray<string> handlerNames, CancellationToken cancellationToken = default)
    {
        await InsertDocumentsAsync(publishedEvent, eventName, handlerNames, DateTime.UtcNow.Add(deferDuration), cancellationToken).ConfigureAwait(false);
    }

    public async Task<IEnumerable<ScheduledEventRecord>> FetchScheduledAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var filter = Builders<EventDocument>.Filter.And(
            Builders<EventDocument>.Filter.Eq(x => x.Status, (int)EventStatus.Scheduled),
            Builders<EventDocument>.Filter.Lte(x => x.ScheduledAt, now));

        var projection = Builders<EventDocument>.Projection
            .Include(x => x.Id)
            .Include(x => x.LastUpdatedAt)
            .Include(x => x.EventName)
            .Include(x => x.HandlerName);

        var sort = Builders<EventDocument>.Sort.Ascending(x => x.ScheduledAt);

        var projected = await _collection
            .Find(filter)
            .Sort(sort)
            .Limit(batchSize)
            .Project<ScheduledProjection>(projection)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if(projected.Count == 0)
        {
            return Array.Empty<ScheduledEventRecord>();
        }

        var records = new List<ScheduledEventRecord>(projected.Count);
        foreach(var item in projected)
        {
            records.Add(new ScheduledEventRecord(item.Id.ToString(), EnsureUtc(item.LastUpdatedAt), item.EventName, item.HandlerName));
        }

        return records;
    }

    public async Task<EventRecord> TryClaimAsync(ScheduledEventRecord scheduledEventRecord, EventRegistry eventRegistry, CancellationToken cancellationToken = default)
    {
        var id = ObjectId.Parse(scheduledEventRecord.Id);
        var now = DateTime.UtcNow;
        var expectedLastUpdatedAt = scheduledEventRecord.LastUpdatedAt;

        var filter = Builders<EventDocument>.Filter.And(
            Builders<EventDocument>.Filter.Eq(x => x.Id, id),
            Builders<EventDocument>.Filter.Eq(x => x.Status, (int)EventStatus.Scheduled),
            Builders<EventDocument>.Filter.Eq(x => x.LastUpdatedAt, expectedLastUpdatedAt));

        var update = Builders<EventDocument>.Update
            .Set(x => x.Status, (int)EventStatus.InProgress)
            .Set(x => x.ClaimedAt, now)
            .Set(x => x.LastUpdatedAt, now);

        var options = new FindOneAndUpdateOptions<EventDocument>
        {
            ReturnDocument = ReturnDocument.After
        };

        var doc = await _collection.FindOneAndUpdateAsync(filter, update, options, cancellationToken).ConfigureAwait(false);

        if(doc is null)
        {
            return EventRecord.Empty;
        }

        object? deserializedEvent = EventSerializer.DeserializePayload(
            scheduledEventRecord.Id, doc.Payload, doc.EventName, eventRegistry, _logger);

        if(deserializedEvent is null)
        {
            return EventRecord.Empty;
        }

        return new EventRecord(
            scheduledEventRecord.Id,
            doc.EventId,
            doc.EventName,
            doc.HandlerName,
            doc.Payload,
            (EventStatus)doc.Status,
            EnsureUtc(doc.ScheduledAt),
            doc.RetryAttemptCount,
            TimeSpan.FromMilliseconds(doc.RetryLastDelay),
            doc.ClaimedAt.HasValue ? EnsureUtc(doc.ClaimedAt.Value) : null,
            EnsureUtc(doc.CreatedAt),
            EnsureUtc(doc.LastUpdatedAt),
            deserializedEvent,
            doc.LastError,
            doc.ProcessingTimeoutsCount);
    }

    public async Task CompleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var docId = ObjectId.Parse(id);
        var now = DateTime.UtcNow;

        var filter = Builders<EventDocument>.Filter.Eq(x => x.Id, docId);
        var update = Builders<EventDocument>.Update
            .Set(x => x.Status, (int)EventStatus.Completed)
            .Set(x => x.LastUpdatedAt, now);

        await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task RetryAsync(string id, int attemptCount, TimeSpan delay, string? error = null, CancellationToken cancellationToken = default)
    {
        var docId = ObjectId.Parse(id);
        var now = DateTime.UtcNow;
        var scheduledAt = now.Add(delay);

        var filter = Builders<EventDocument>.Filter.Eq(x => x.Id, docId);
        var update = Builders<EventDocument>.Update
            .Set(x => x.Status, (int)EventStatus.Scheduled)
            .Set(x => x.ScheduledAt, scheduledAt)
            .Set(x => x.RetryAttemptCount, attemptCount)
            .Set(x => x.RetryLastDelay, (long)delay.TotalMilliseconds)
            .Set(x => x.LastUpdatedAt, now)
            .Set(x => x.LastError, error);

        await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task DeadLetterAsync(string id, string? error = null, CancellationToken cancellationToken = default)
    {
        var docId = ObjectId.Parse(id);
        var now = DateTime.UtcNow;

        var filter = Builders<EventDocument>.Filter.Eq(x => x.Id, docId);
        var update = Builders<EventDocument>.Update
            .Set(x => x.Status, (int)EventStatus.DeadLettered)
            .Set(x => x.LastUpdatedAt, now)
            .Set(x => x.LastError, error);

        await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task RescheduleClaimedExceedingProcessingTimeoutAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var claimedBefore = now.Subtract(_settings.ProcessingTimeout);
        var maxProcessingTimeouts = _settings.MaxProcessingTimeouts;

        // Use an aggregation pipeline update to handle both cases in a single pass:
        // - records at or above MaxProcessingTimeouts → dead-letter
        // - records below MaxProcessingTimeouts → reschedule (increment timeout count, clear claimed_at)
        var filter = Builders<EventDocument>.Filter.And(
            Builders<EventDocument>.Filter.Eq(x => x.Status, (int)EventStatus.InProgress),
            Builders<EventDocument>.Filter.Lte(x => x.ClaimedAt, claimedBefore));

        var pipeline = new EmptyPipelineDefinition<EventDocument>()
            .AppendStage<EventDocument, EventDocument, EventDocument>(
                new BsonDocumentPipelineStageDefinition<EventDocument, EventDocument>(
                    new BsonDocument("$set", new BsonDocument
                    {
                        ["status"] = new BsonDocument("$cond", new BsonArray
                        {
                            new BsonDocument("$gte", new BsonArray { "$processing_timeouts_count", maxProcessingTimeouts }),
                            (int)EventStatus.DeadLettered,
                            (int)EventStatus.Scheduled
                        }),
                        ["scheduled_at"] = new BsonDocument("$cond", new BsonArray
                        {
                            new BsonDocument("$gte", new BsonArray { "$processing_timeouts_count", maxProcessingTimeouts }),
                            "$scheduled_at",
                            now
                        }),
                        ["claimed_at"] = new BsonDocument("$cond", new BsonArray
                        {
                            new BsonDocument("$gte", new BsonArray { "$processing_timeouts_count", maxProcessingTimeouts }),
                            "$claimed_at",
                            BsonNull.Value
                        }),
                        ["processing_timeouts_count"] = new BsonDocument("$cond", new BsonArray
                        {
                            new BsonDocument("$gte", new BsonArray { "$processing_timeouts_count", maxProcessingTimeouts }),
                            "$processing_timeouts_count",
                            new BsonDocument("$add", new BsonArray { "$processing_timeouts_count", 1 })
                        }),
                        ["last_updated_at"] = now,
                        ["last_error"] = new BsonDocument("$cond", new BsonArray
                        {
                            new BsonDocument("$gte", new BsonArray { "$processing_timeouts_count", maxProcessingTimeouts }),
                            "Max processing timeouts count reached",
                            "$last_error"
                        })
                    })));

        await _collection.UpdateManyAsync(filter, pipeline, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task DeadLetterUnclaimedAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var scheduledBefore = now.Subtract(_settings.UnclaimedTtl);

        var filter = Builders<EventDocument>.Filter.And(
            Builders<EventDocument>.Filter.Eq(x => x.Status, (int)EventStatus.Scheduled),
            Builders<EventDocument>.Filter.Lte(x => x.ScheduledAt, scheduledBefore));

        var update = Builders<EventDocument>.Update
            .Set(x => x.Status, (int)EventStatus.DeadLettered)
            .Set(x => x.LastUpdatedAt, now)
            .Set(x => x.LastError, "Unclaimed event");

        await _collection.UpdateManyAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteCompletedAndDeadLetteredExceedingTtlAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var completedBefore = now.Subtract(_settings.CompletedRecordTtl);
        var deadLetteredBefore = now.Subtract(_settings.DeadLetteredRecordTtl);

        var completedFilter = Builders<EventDocument>.Filter.And(
            Builders<EventDocument>.Filter.Eq(x => x.Status, (int)EventStatus.Completed),
            Builders<EventDocument>.Filter.Lte(x => x.LastUpdatedAt, completedBefore));

        var deadLetteredFilter = Builders<EventDocument>.Filter.And(
            Builders<EventDocument>.Filter.Eq(x => x.Status, (int)EventStatus.DeadLettered),
            Builders<EventDocument>.Filter.Lte(x => x.LastUpdatedAt, deadLetteredBefore));

        await _collection.DeleteManyAsync(
            Builders<EventDocument>.Filter.Or(completedFilter, deadLetteredFilter),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task InsertDocumentsAsync<TEvent>(TEvent publishedEvent, string eventName, ImmutableArray<string> handlerNames, DateTime scheduledAt, CancellationToken cancellationToken)
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

        await _collection.InsertManyAsync(documents, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private void EnsureIndexes()
    {
        var indexModels = new[]
        {
            new CreateIndexModel<EventDocument>(
                Builders<EventDocument>.IndexKeys
                    .Ascending(x => x.Status)
                    .Ascending(x => x.ScheduledAt),
                new CreateIndexOptions { Background = true }),
            new CreateIndexModel<EventDocument>(
                Builders<EventDocument>.IndexKeys
                    .Ascending(x => x.Status)
                    .Ascending(x => x.ClaimedAt),
                new CreateIndexOptions { Background = true }),
            new CreateIndexModel<EventDocument>(
                Builders<EventDocument>.IndexKeys
                    .Ascending(x => x.Status)
                    .Ascending(x => x.LastUpdatedAt),
                new CreateIndexOptions { Background = true })
        };

        _collection.Indexes.CreateMany(indexModels);
    }

    private static DateTime EnsureUtc(DateTime dt) => dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);
}
