using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using M.EventBrokerSlim.Persistent;
using Microsoft.Extensions.Logging;

namespace M.EventBrokerSlim.Internal.Persistent;

internal class EventStoragePolling
{
    private readonly PersistentEventBrokerSettings _settings;
    private readonly Channel<ScheduledEventRecord> _handlerRunnerChannel;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly PollRequiredSignal _pollRequiredSignal;
    private readonly ILogger _logger;
    private readonly IEventStorage _eventStorage;

    public EventStoragePolling(
        PersistentEventBrokerSettings settings,
        IEventStorage eventStorage,
        Channel<ScheduledEventRecord> handlerRunnerChannel,
        PollRequiredSignal pollRequiredSignal,
        CancellationTokenSource cancellationTokenSource,
        ILogger logger)
    {
        _settings = settings;
        _eventStorage = eventStorage;
        _handlerRunnerChannel = handlerRunnerChannel;
        _cancellationTokenSource = cancellationTokenSource;
        _pollRequiredSignal = pollRequiredSignal;
        _logger = logger;
    }

    public void Run()
    {
        _ = Task.Factory.StartNew(PollEventStorageAsync, TaskCreationOptions.LongRunning | TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private async Task PollEventStorageAsync()
    {
        var cancellationToken = _cancellationTokenSource.Token;
        while(!cancellationToken.IsCancellationRequested)
        {
            try
            {
                _pollRequiredSignal.Reset();
                IEnumerable<ScheduledEventRecord> eventRecords =
                    await _eventStorage.TryFetchScheduledAsync(_settings.ScheduledBatchSize, _logger, cancellationToken).ConfigureAwait(false);
                foreach(ScheduledEventRecord eventRecord in eventRecords)
                {
                    await _handlerRunnerChannel.Writer.WriteAsync(eventRecord, cancellationToken).ConfigureAwait(false);
                }

                // If we fetched less than the batch size, it's likely there are no more ready events, so wait for a signal or timeout before polling again
                if(eventRecords.Count() != _settings.ScheduledBatchSize)
                {
                    await _pollRequiredSignal.WaitForSignalAsync(_settings.PollingInterval, cancellationToken).ConfigureAwait(false);
                }
            }
            catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "An error occurred while polling event storage.");
                await Task.Delay(_settings.PollingInterval, cancellationToken);
            }
        }
    }
}
