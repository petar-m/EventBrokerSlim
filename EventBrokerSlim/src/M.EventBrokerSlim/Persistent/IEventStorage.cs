using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace M.EventBrokerSlim.Persistent;

/// <summary>
/// Represents the contract for event storage operations in a persistent event broker.
/// </summary>
public interface IEventStorage
{
    /// <summary>
    /// Schedules the specified event for asynchronous processing by the provided event handlers. 
    /// </summary>
    /// <remarks>This method is typically called at the time an event is published. It schedules the event for
    /// each handler by writing a record per handler name.</remarks>
    /// <typeparam name="TEvent">The type of the event to be published and scheduled for handling.</typeparam>
    /// <param name="publishedEvent">The event instance to be published and scheduled for processing. Cannot be null.</param>
    /// <param name="eventName">The name of the event being published. Used for logging and identification purposes. Cannot be null or empty.</param>
    /// <param name="handlerNames">An immutable array containing the names of the handlers that will process the published event. Each name must
    /// correspond to a valid handler.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the scheduling operation.</param>
    /// <returns>A task that represents the asynchronous operation of scheduling the event for each specified handler.</returns>
    Task ScheduleAsync<TEvent>(TEvent publishedEvent, string eventName, ImmutableArray<string> handlerNames, CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedules the specified event to be processed after a specified delay.
    /// </summary>
    /// <remarks>This method is typically called when an event needs to be deferred for a certain period before processing.</remarks>
    /// <typeparam name="TEvent">The type of the event to be published.</typeparam>
    /// <param name="publishedEvent">The event instance to be scheduled for deferred processing.</param>
    /// <param name="deferDuration">The amount of time to wait before the event is processed.</param>
    /// <param name="eventName">The name of the event being scheduled.</param>
    /// <param name="handlerNames">An array containing the names of the handlers that will process the event after the delay.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the scheduling operation.</param>
    /// <returns>A task that represents the asynchronous scheduling operation.</returns>
    Task ScheduleDeferredAsync<TEvent>(TEvent publishedEvent, TimeSpan deferDuration, string eventName, ImmutableArray<string> handlerNames, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves a batch of event records that are scheduled and due for processing.
    /// </summary>
    /// <remarks>This method fetches event records with a status of 'Scheduled' and a scheduled time less than
    /// or equal to the current time. The actual batch size may be limited by backend configuration. No filtering is
    /// applied based on handler name.</remarks>
    /// <param name="batchSize">The maximum number of scheduled event records to fetch in a single operation. Must be a positive integer.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains event records that are
    /// scheduled for processing and are due at the time of the call.</returns>
    Task<IEnumerable<ScheduledEventRecord>> FetchScheduledAsync(int batchSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to claim a candidate record identified by the specified ID using optimistic concurrency. The claim is
    /// successful only if the candidate's status remains scheduled at the time of the update.
    /// </summary>
    /// <remarks>This method is typically called by a background poller and sets the candidate's status to
    /// 'InProgress' and the claimed timestamp to the current time only if the status remains 'Scheduled'.</remarks>
    /// <param name="scheduledEventRecord">A scheduled event record that is a candidate for claiming.</param>
    /// <param name="eventRegistry">The registry containing event type information.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation if needed.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the claimed event record if the claim is successful, or ScheduledEventRecord.Empty if the claim fails (e.g., due to a concurrency conflict or if the record is no longer scheduled).</returns>
    Task<EventRecord> TryClaimAsync(ScheduledEventRecord scheduledEventRecord, EventRegistry eventRegistry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the status of the candidate record identified by the specified ID to 'Completed' after successful execution of the event handler.
    /// </summary>
    /// <param name="id">The unique identifier of the candidate record to be marked as completed.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation if needed.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task CompleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedules a retry for the candidate record identified by the specified ID after a failure in event handler execution. This method updates the candidate's status to 'Scheduled', retry attempt count, the delay before the next retry attempt, and optionally records an error message describing the failure.
    /// </summary>
    /// <param name="id">The unique identifier of the candidate record to be retried.</param>
    /// <param name="attemptCount">The number of retry attempts that have been made.</param>
    /// <param name="delay">The delay before the next retry attempt.</param>
    /// <param name="error">An optional error message describing the failure.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation if needed.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task RetryAsync(string id, int attemptCount, TimeSpan delay, string? error = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the status of the candidate record identified by the specified ID to 'DeadLettered' after the retry policy is exhausted.
    /// </summary>
    /// <param name="id">The unique identifier of the candidate record to be dead-lettered.</param>
    /// <param name="error">An optional error message describing the failure.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation if needed.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task DeadLetterAsync(string id, string? error = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the status of claimed records that have exceeded the processing timeout back to 'Scheduled',
    /// making them available for claiming by other instances. 
    /// <remarks>This method is typically called by a background maintenance task to handle records that may be stuck due to failures or long processing times.
    /// The method should also increment the processing timeout count for each affected record and optionally log or handle records that exceed a maximum number of processing timeouts, as defined in the settings.</remarks>
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation if needed.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task RescheduleClaimedExceedingProcessingTimeoutAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Dead-letters unclaimed records that have exceeded the unclaimed TTL, making them available for inspection and manual handling.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation if needed.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task DeadLetterUnclaimedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes completed and dead-lettered records that have exceeded their TTL, freeing up storage and maintaining database hygiene.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation if needed.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task DeleteCompletedAndDeadLetteredExceedingTtlAsync(CancellationToken cancellationToken = default);
}
