using System;

namespace M.EventBrokerSlim.Persistent;

/// <summary>
/// Configuration for background maintenance operations (timeouts, TTLs and cleanup).
/// </summary>
public class PersistentEventBrokerSettings
{
    /// <summary>
    /// How long a Scheduled record that has not been claimed is retained before being moved to DeadLettered.
    /// The window is measured from <c>scheduled_at</c> — the time the record first became claimable —
    /// not from when it was created. This ensures deferred publishes receive a full unclaimed window
    /// from the moment they become eligible for dispatch.
    /// Default: 7 days.
    /// </summary>
    public TimeSpan UnclaimedTtl { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// How long a record may remain InProgress before being considered stuck.
    /// Records exceeding this threshold are reset to Scheduled with scheduled_at = now,
    /// making them immediately available for re-claiming by another instance.
    /// Must be set longer than the longest expected handler execution time, including retry delays.
    /// Default: 5 minutes.
    /// </summary>
    public TimeSpan ProcessingTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How long Completed records are retained before being deleted.
    /// Completed records have no further purpose beyond short-term observability.
    /// Default: 7 days.
    /// </summary>
    public TimeSpan CompletedRecordTtl { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// How long DeadLettered records are retained before being deleted.
    /// DeadLettered records require operator attention — inspection, requeue, or acknowledgement.
    /// Should be set long enough to give operators time to act.
    /// Default: 30 days.
    /// </summary>
    public TimeSpan DeadLetteredRecordTtl { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Gets or sets the maximum number of processing timeouts allowed before an operation is considered failed.
    /// Default: 10.
    /// </summary>
    /// <remarks>This property controls the threshold for processing timeouts, which can affect the
    /// reliability of operations that depend on timely execution. Adjusting this value may be necessary based on the
    /// specific requirements of the application or the expected performance characteristics of the
    /// environment.</remarks>
    public int MaxProcessingTimeouts { get; set; } = 10;

    /// <summary>
    /// The maximum number of scheduled event records to fetch in a single batch.
    /// Default: 10.
    /// </summary>
    public int ScheduledBatchSize { get; set; } = 10;

    /// <summary>
    /// Gets or sets the interval at which polling operations are performed.
    /// </summary>
    /// <remarks>The default value is 10 seconds. Adjusting this interval affects how frequently polling occurs.</remarks>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets the interval configuration for executing dead-lettering of unclaimed messages exceeding <see cref="UnclaimedTtl"/>. Default is a jittered interval with an average of 3 minutes and a maximum variation of 2 minutes for the initial phase, and an average of 60 minutes with a maximum variation of 10 minutes for the regular phase.
    /// </summary>
    /// <remarks>This property determines how frequently the system attempts to move messages
    /// that remain unclaimed in the dead-letter queue. Adjust the interval to balance timely processing with system
    /// resource usage.</remarks>
    public Jitter DeadLetterUnclaimedExecuteInterval { get; set; } = new Jitter(new Jitter.Periodic(TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(2)), new Jitter.Periodic(TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(10)));

    /// <summary>
    /// Gets or sets the interval at which completed and dead-lettered messages exceeding their time-to-live (TTL) are
    /// deleted. (See <seealso cref="CompletedRecordTtl"/> and <seealso cref="DeadLetteredRecordTtl"/>). Default is a jittered interval with an average of 3 minutes and a maximum variation of 2 minutes for the initial phase, and an average of 60 minutes with a maximum variation of 10 minutes for the regular phase.
    /// </summary>
    /// <remarks>This property determines how frequently the system executes cleanup operations to remove
    /// messages that have been completed or dead-lettered and have exceeded their configured TTL. Adjusting this
    /// interval can affect resource usage and the timeliness of message cleanup.</remarks>
    public Jitter DeleteCompletedAndDeadLetteredExceedingTtlExecuteInterval { get; set; } = new Jitter(new Jitter.Periodic(TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(2)), new Jitter.Periodic(TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(10)));

    /// <summary>
    /// Gets or sets the interval used to reschedule claimed items that have exceeded their processing timeout <see cref="ProcessingTimeout"/>. Default is a jittered interval with an average of 10 seconds and a maximum variation of 7 seconds for the initial phase, and an average of 5 minutes with a maximum variation of 1 minute for the regular phase.
    /// </summary>
    /// <remarks>This property determines how frequently the system attempts to reschedule items that remain
    /// claimed beyond their allowed processing time. Adjusting this interval can affect how quickly overdue items are
    /// retried and may impact overall throughput and resource usage.</remarks>
    public Jitter RescheduleClaimedExceedingProcessingTimeoutExecuteInterval { get; set; } = new Jitter(new Jitter.Periodic(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(7)), new Jitter.Periodic(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1)));
}

