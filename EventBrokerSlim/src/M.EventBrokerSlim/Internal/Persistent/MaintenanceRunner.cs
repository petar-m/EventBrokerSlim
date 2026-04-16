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

        Run(async () => await _eventStorage.DeadLetterUnclaimedAsync(cancellationToken),
            _settings.DeadLetterUnclaimedExecuteInterval,
            "Error dead-lettering unclaimed events exceeding TTL.",
            cancellationToken);

        Run(async () => await _eventStorage.DeleteCompletedAndDeadLetteredExceedingTtlAsync(cancellationToken).ConfigureAwait(false),
            _settings.DeleteCompletedAndDeadLetteredExceedingTtlExecuteInterval,
            "Error deleting completed and dead-lettered events exceeding TTL.",
            cancellationToken);

        Run(async () => await _eventStorage.RescheduleClaimedExceedingProcessingTimeoutAsync(cancellationToken).ConfigureAwait(false),
            _settings.RescheduleClaimedExceedingProcessingTimeoutExecuteInterval,
            "Error rescheduling claimed events exceeding processing timeout.",
            cancellationToken);
    }

    private void Run(Func<Task> task, Jitter jitter, string errorMessage, CancellationToken cancellationToken)
    {
        _ = Task.Run(
            async () =>
            {
                TimeSpan delay = jitter.Initial.GetNext();
                do
                {
                    try
                    {
                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                        await task().ConfigureAwait(false);
                    }
                    catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch(Exception ex)
                    {
                        _logger.LogError(ex, errorMessage);
                    }

                    delay = jitter.Regular.GetNext();
                } while(!cancellationToken.IsCancellationRequested);
            },
            cancellationToken);
    }
}
