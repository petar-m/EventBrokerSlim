using System;

namespace M.EventBrokerSlim;

/// <summary>
/// Describes a retry request for re-processing an event.
/// </summary>
public class RetryPolicy
{
    private TimeSpan _delay;

    internal RetryPolicy()
    {
    }

    /// <summary>
    /// Requests invoking of the same handler with the same event after given time interval.
    /// </summary>
    /// <param name="delay">The time interval to wait before re-processing.</param>
    public void RetryAfter(TimeSpan delay)
    {
        _delay = delay;
        RetryRequested = true;
    }

    /// <summary>
    /// Requests invoking of the same handler with the same event after given time interval.
    /// </summary>
    /// <param name="delay">A func taking the attempt number for the same handler and event and the last retry interval and returning the new wait interval before re-processing.</param>
    public void RetryAfter(Func<uint, TimeSpan, TimeSpan> delay)
    {
        _delay = delay(Attempt, _delay);
        RetryRequested = true;
    }

    /// <summary>
    /// Current attempt for the same handler and event.
    /// </summary>
    public uint Attempt { get; private set; }

    /// <summary>
    /// The time interval delay used for the last re-processing.
    /// </summary>
    public TimeSpan LastDelay => _delay;

    /// <summary>
    /// Indicates whether a re-processing has been requested for the handler and event.
    /// </summary>
    public bool RetryRequested { get; private set; }

    internal void NextAttempt()
    {
        Attempt++;
        RetryRequested = false;
    }

    internal void Clear()
    {
        Attempt = 0;
        _delay = TimeSpan.Zero;
        RetryRequested = false;
    }
}
