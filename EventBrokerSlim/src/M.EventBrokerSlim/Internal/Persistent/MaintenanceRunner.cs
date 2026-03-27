using System;
using System.Threading;
using System.Threading.Tasks;
using M.EventBrokerSlim.Persistent;
using Microsoft.Extensions.Logging;

namespace M.EventBrokerSlim.Internal.Persistent;

internal class MaintenanceRunner
{
    private readonly IEventStorage _eventStorage;
    private readonly PersistentEventBrokerSettings _settings;
    private readonly ILogger<MaintenanceRunner> _logger;
    private readonly CancellationTokenSource _cancellationTokenSource;

    public MaintenanceRunner(
        IEventStorage eventStorage,
        PersistentEventBrokerSettings settings,
        ILogger<MaintenanceRunner> logger,
        CancellationTokenSource cancellationTokenSource)
    {
        _eventStorage = eventStorage;
        _settings = settings;
        _logger = logger;
        _cancellationTokenSource = cancellationTokenSource;
    }

    public void Run()
    {
        var cancellationToken = _cancellationTokenSource.Token;
        _ = Task.Run(
            async () =>
            {
                var jitter = TimeSpan.FromMinutes(Random.Shared.Next(1, 5));
                do
                {
                    try
                    {
                        await Task.Delay(jitter, cancellationToken).ConfigureAwait(false);
                        await _eventStorage.DeadLetterUnclaimedAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch(Exception ex)
                    {
                        _logger.LogError(ex, "Error dead-lettering unclaimed events exceeding TTL.");
                    }

                    jitter = TimeSpan.FromMinutes(60 + Random.Shared.Next(-10, 10));
                } while(!cancellationToken.IsCancellationRequested);
            },
            cancellationToken);

        _ = Task.Run(
            async () =>
            {
                var jitter = TimeSpan.FromMinutes(Random.Shared.Next(1, 5));
                do
                {
                    try
                    {
                        await Task.Delay(jitter, cancellationToken).ConfigureAwait(false);
                        await _eventStorage.DeleteCompletedAndDeadLetteredExceedingTtlAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch(Exception ex)
                    {
                        _logger.LogError(ex, "Error deleting completed and dead-lettered events exceeding TTL.");
                    }

                    jitter = TimeSpan.FromMinutes(60 + Random.Shared.Next(-10, 10));
                } while(!cancellationToken.IsCancellationRequested);
            },
            cancellationToken);

        var processingTimeoutMinutes = Math.Max(2, (int)_settings.ProcessingTimeout.TotalMinutes);
        _ = Task.Run(
            async () =>
            {
                var jitter = TimeSpan.FromMinutes(Random.Shared.Next(1, 5));
                do
                {
                    try
                    {
                        await Task.Delay(jitter, cancellationToken).ConfigureAwait(false);
                        await _eventStorage.RescheduleClaimedExceedingProcessingTimeoutAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch(Exception ex)
                    {
                        _logger.LogError(ex, "Error rescheduling claimed events exceeding processing timeout.");
                    }

                    jitter = TimeSpan.FromMinutes(Random.Shared.Next(1, processingTimeoutMinutes));
                } while(!cancellationToken.IsCancellationRequested);
            },
            cancellationToken);
    }
}
