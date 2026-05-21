using System;

namespace M.EventBrokerSlim.PersistentEvents.LiteDb.Internal;

internal class EventDocument
{
    public long Id { get; set; }
    public string EventId { get; set; } = null!;
    public string EventName { get; set; } = null!;
    public string HandlerName { get; set; } = null!;
    public string Payload { get; set; } = null!;
    public int Status { get; set; }
    public DateTime ScheduledAt { get; set; }
    public int RetryAttemptCount { get; set; }
    public long RetryLastDelay { get; set; }
    public DateTime? ClaimedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    public string? LastError { get; set; }
    public int ProcessingTimeoutsCount { get; set; }
}

internal class ScheduledProjection
{
    public long Id { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    public string EventName { get; set; } = null!;
    public string HandlerName { get; set; } = null!;
}
