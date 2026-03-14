using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using M.EventBrokerSlim.Persistent;
using Microsoft.Extensions.Logging;

internal static class IEventStorageExtensions
{
    public static async Task<IEnumerable<EventRecord>> TryFetchScheduledAsync(this IEventStorage eventStorage, int batchSize, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            return await eventStorage.FetchScheduledAsync(batchSize, cancellationToken).ConfigureAwait(false);
        }
        catch(Exception ex)
        {
            // TODO: Consider more specific exception types for better error handling.
            logger.LogError(ex, "Error fetching scheduled events.");
            return Array.Empty<EventRecord>();
        }
    }

    public static async Task<bool> TryClaimAsync(this IEventStorage eventStorage, string id, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            return await eventStorage.TryClaimAsync(id, cancellationToken).ConfigureAwait(false);
        }
        catch(Exception ex)
        {
            logger.LogError(ex, "Error claiming event with ID {EventId}.", id);
            return false;
        }
    }

    public static async Task TryDeadLetterAsync(this IEventStorage eventStorage, string id, string reason, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            await eventStorage.DeadLetterAsync(id, reason, cancellationToken).ConfigureAwait(false);
        }
        catch(Exception ex)
        {
            logger.LogError(ex, "Error dead-lettering event with ID {EventId}.", id);
        }
    }

    public static async Task TryRetryAsync(this IEventStorage eventStorage, string id, int attemptCount, TimeSpan delay, ILogger logger, CancellationToken cancellationToken, string? error = null)
    {
        try
        {
            await eventStorage.RetryAsync(id, attemptCount, delay, error, cancellationToken).ConfigureAwait(false);
        }
        catch(Exception ex)
        {
            logger.LogError(ex, "Error rescheduling event with ID {EventId}.", id);
        }
    }

    public static async Task TryCompleteAsync(this IEventStorage eventStorage, string id, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            await eventStorage.CompleteAsync(id, cancellationToken).ConfigureAwait(false);
        }
        catch(Exception ex)
        {
            logger.LogError(ex, "Error completing event with ID {EventId}.", id);
        }
    }
}
