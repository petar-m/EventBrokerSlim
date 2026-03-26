using System;

namespace M.EventBrokerSlim.Persistent;

/// <summary>
/// Represents a persistent record of an event, including its identity, processing status, scheduling, and error
/// information.
/// </summary>
/// <param name="Id">The unique identifier for the event record.</param>
/// <param name="EventId">The identifier associated with the specific event instance.</param>
/// <param name="EventName">The name of the event being recorded.</param>
/// <param name="HandlerName">The name of the handler responsible for processing the event.</param>
/// <param name="Payload">The serialized data payload associated with the event. Determined by IEventStorage implementation. Typically a JSON string or binary data.</param>
/// <param name="Status">The current processing status of the event.</param>
/// <param name="ScheduledAt">The date and time when the event is scheduled to be processed.</param>
/// <param name="RetryAttemptCount">The number of times processing the event has been retried.</param>
/// <param name="RetryLastDelay">The duration of the most recent delay before retrying event processing.</param>
/// <param name="ClaimedAt">The date and time when the event was last claimed for processing, or null if it has not been claimed.</param>
/// <param name="CreatedAt">The date and time when the event record was created.</param>
/// <param name="LastUpdatedAt">The date and time when the event record was last updated.</param>
/// <param name="DeserializedEvent">The deserialized event object, or null if it has not been deserialized.</param>
/// <param name="LastError">The error message from the most recent failed processing attempt, or null if no error has occurred.</param>
/// <param name="ProcessingTimeoutsCount">The number of times the event processing has timed out.</param>
public record EventRecord(
    string Id,
    string EventId,
    string EventName,
    string HandlerName,
    object Payload,
    EventStatus Status,
    DateTime ScheduledAt,
    int RetryAttemptCount,
    TimeSpan RetryLastDelay,
    DateTime? ClaimedAt,
    DateTime CreatedAt,
    DateTime LastUpdatedAt,
    object DeserializedEvent,
    string? LastError,
    int ProcessingTimeoutsCount = 0)
{
    /// <summary>
    /// Represents an empty event record, which can be used as a default value when an event record cannot be retrieved or claimed.
    /// This instance has all properties set to their default values (e.g., empty strings, zero counts, minimum date values) and is intended to signify the absence of a valid event record.
    /// </summary>
    public static readonly EventRecord Empty = new EventRecord(
        Id: string.Empty,
        EventId: string.Empty,
        EventName: string.Empty,
        HandlerName: string.Empty,
        Payload: new object(),
        Status: EventStatus.Scheduled,
        ScheduledAt: DateTime.MinValue,
        RetryAttemptCount: 0,
        RetryLastDelay: TimeSpan.Zero,
        ClaimedAt: null,
        CreatedAt: DateTime.MinValue,
        LastUpdatedAt: DateTime.MinValue,
        DeserializedEvent: null!,
        LastError: null);
}

/// <summary>
/// Represents a record of a scheduled event, containing essential information for identifying and processing the event when it is due. This record is typically used for fetching scheduled events that are ready to be processed by the event handlers. It includes the unique identifier of the scheduled event, the last updated timestamp for concurrency control, the name of the event, and the name of the handler responsible for processing it.
/// </summary>
/// <param name="Id">The unique identifier of the event record.</param>
/// <param name="LastUpdatedAt">The timestamp of the last update to the event, used for concurrency control.</param>
/// <param name="EventName">The name of the event.</param>
/// <param name="HandlerName">The name of the handler responsible for processing the event.</param>
public record ScheduledEventRecord(string Id, DateTime LastUpdatedAt, string EventName, string HandlerName);
