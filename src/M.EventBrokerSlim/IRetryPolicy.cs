using System;

namespace M.EventBrokerSlim;

/// <summary>
/// Represents a retry request for re-processing an event.
/// </summary>
public interface IRetryPolicy
{
    /// <summary>
    /// Current attempt for the same handler and event.
    /// </summary>
    uint Attempt { get; }

    /// <summary>
    /// The time interval delay used for the last re-processing.
    /// </summary>
    TimeSpan LastDelay { get; }

    /// <summary>
    /// Indicates whether a re-processing has been requested for the handler and event.
    /// </summary>
    bool RetryRequested { get; }

    /// <summary>
    /// Requests invoking of the same handler with the same event after given time interval.
    /// </summary>
    /// <param name="delay">A func taking the attempt number for the same handler and event and the last retry interval and returning the new wait interval before re-processing.</param>
    void RetryAfter(Func<uint, TimeSpan, TimeSpan> delay);

    /// <summary>
    /// Requests invoking of the same handler with the same event after given time interval.
    /// </summary>
    /// <param name="delay">The time interval to wait before re-processing.</param>
    void RetryAfter(TimeSpan delay);
}
