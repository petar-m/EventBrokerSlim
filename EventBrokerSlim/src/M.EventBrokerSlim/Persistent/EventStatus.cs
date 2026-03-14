namespace M.EventBrokerSlim.Persistent;

/// <summary>
/// Represents the processing status of a persistent event.
/// </summary>
public enum EventStatus
{
    /// <summary>
    /// Represents an unknown state or value. This is the default value and should not be used in normal operation. It can be used to indicate an uninitialized or invalid state.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Represents a scheduled state, indicating that the event is scheduled for processing.
    /// </summary>
    Scheduled = 1,

    /// <summary>
    /// Represents an in-progress state, indicating that the event is currently being processed.
    /// </summary>
    InProgress = 2,

    /// <summary>
    /// Represents a completed state, indicating that the event has been successfully processed.
    /// </summary>
    Completed = 3,

    /// <summary>
    /// Represents a dead-lettered state, indicating that the event could not be processed successfully and has been moved to a dead-letter queue.
    /// </summary>
    DeadLettered = 4,
}
