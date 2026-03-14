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
/// <param name="Payload">The serialized data payload associated with the event.</param>
/// <param name="Status">The current processing status of the event.</param>
/// <param name="ScheduledAt">The date and time when the event is scheduled to be processed.</param>
/// <param name="RetryAttemptCount">The number of times processing the event has been retried.</param>
/// <param name="RetryLastDelay">The duration of the most recent delay before retrying event processing.</param>
/// <param name="ClaimedAt">The date and time when the event was last claimed for processing, or null if it has not been claimed.</param>
/// <param name="CreatedAt">The date and time when the event record was created.</param>
/// <param name="LastUpdatedAt">The date and time when the event record was last updated.</param>
/// <param name="LastError">The error message from the most recent failed processing attempt, or null if no error has occurred.</param>
public record EventRecord(
    string Id,
    string EventId,
    string EventName,
    string HandlerName,
    string Payload,
    EventStatus Status,
    DateTime ScheduledAt,
    int RetryAttemptCount,
    TimeSpan RetryLastDelay,
    DateTime? ClaimedAt,
    DateTime CreatedAt,
    DateTime LastUpdatedAt,
    string? LastError);
