using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace M.EventBrokerSlim.PersistentEvents.MongoDb.Internal;

internal class EventDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("event_id")]
    public string EventId { get; set; } = null!;

    [BsonElement("event_name")]
    public string EventName { get; set; } = null!;

    [BsonElement("handler_name")]
    public string HandlerName { get; set; } = null!;

    [BsonElement("payload")]
    public string Payload { get; set; } = null!;

    [BsonElement("status")]
    public int Status { get; set; }

    [BsonElement("scheduled_at")]
    public DateTime ScheduledAt { get; set; }

    [BsonElement("retry_attempt_count")]
    public int RetryAttemptCount { get; set; }

    [BsonElement("retry_last_delay")]
    public long RetryLastDelay { get; set; }

    [BsonElement("claimed_at")]
    [BsonIgnoreIfNull]
    public DateTime? ClaimedAt { get; set; }

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("last_updated_at")]
    public DateTime LastUpdatedAt { get; set; }

    [BsonElement("last_error")]
    [BsonIgnoreIfNull]
    public string? LastError { get; set; }

    [BsonElement("processing_timeouts_count")]
    public int ProcessingTimeoutsCount { get; set; }
}

internal class ScheduledProjection
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("last_updated_at")]
    public DateTime LastUpdatedAt { get; set; }

    [BsonElement("event_name")]
    public string EventName { get; set; } = null!;

    [BsonElement("handler_name")]
    public string HandlerName { get; set; } = null!;
}
