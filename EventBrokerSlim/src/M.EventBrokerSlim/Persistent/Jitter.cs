using System;

namespace M.EventBrokerSlim.Persistent;

/// <summary>
/// Represents a jitter configuration with separate initial and regular periodic intervals, each supporting random
/// variation.
/// </summary>
/// <remarks>Use this class to specify timing strategies that introduce randomness (jitter) to periodic
/// operations, such as retry or polling mechanisms. The initial and regular intervals can be configured independently
/// to control the timing and variability of repeated actions.</remarks>
public class Jitter
{
    /// <summary>
    /// Initializes a new instance of the Jitter class with the specified initial and regular periodic intervals.
    /// </summary>
    /// <param name="initial">The initial periodic interval to use before switching to the regular interval. Cannot be null.</param>
    /// <param name="regular">The regular periodic interval to use after the initial interval. Cannot be null.</param>
    public Jitter(Periodic initial, Periodic regular)
    {
        Initial = initial;
        Regular = regular;
    }

    /// <summary>
    /// Gets the initial periodic value used to start the sequence or calculation.
    /// </summary>
    public Periodic Initial { get; }

    /// <summary>
    /// Gets the standard periodic schedule used for recurring operations.
    /// </summary>
    public Periodic Regular { get; }

    /// <summary>
    /// Represents a periodic time interval with optional random variation applied to each occurrence.
    /// </summary>
    /// <remarks>Use this class to generate time intervals that repeat at a regular cadence, with an optional
    /// amount of jitter to avoid exact periodicity. This can be useful for scheduling recurring operations where slight
    /// randomization helps prevent synchronization issues or contention.</remarks>
    public class Periodic
    {
        /// <summary>
        /// Initializes a new instance of the Periodic class with the specified interval and variation.
        /// </summary>
        /// <param name="interval">The base time interval between each occurrence.</param>
        /// <param name="variation">The maximum amount of time by which the interval can vary.</param>
        public Periodic(TimeSpan interval, TimeSpan variation)
        {
            Interval = interval;
            Variation = variation;
        }

        /// <summary>
        /// Gets the time interval between consecutive operations or events.
        /// </summary>
        public TimeSpan Interval { get; }

        /// <summary>
        /// Gets the allowed variation in time for the associated operation or event.
        /// </summary>
        public TimeSpan Variation { get; }

        /// <summary>
        /// Calculates the next interval by applying a random jitter to the base interval.
        /// </summary>
        /// <remarks>The returned interval is computed by adding or subtracting up to the specified
        /// variation from the base interval. This can be used to randomize periodic operations and avoid
        /// synchronization issues in distributed systems.</remarks>
        /// <returns>A <see cref="TimeSpan"/> representing the next interval with jitter applied. The value is never less than
        /// <see cref="TimeSpan.Zero"/>.</returns>
        public TimeSpan GetNext()
        {
            var jitterTicks = (long)(Variation.Ticks * (Random.Shared.NextDouble() - 0.5) * 2);
            TimeSpan timeSpan = Interval + TimeSpan.FromTicks(jitterTicks);
            return timeSpan < TimeSpan.Zero ? TimeSpan.Zero : timeSpan;
        }
    }
}
