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
    private readonly Channel<EventRecord> _handlerRunnerChannel;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly PollRequiredSignal _pollRequiredSignal;
    private readonly ILogger _logger;
    private readonly IEventStorage _eventStorage;

    public EventStoragePolling(
        PersistentEventBrokerSettings settings,
        IEventStorage eventStorage,
        Channel<EventRecord> handlerRunnerChannel,
        CancellationTokenSource cancellationTokenSource,
        ILogger logger)
    {
        _settings = settings;
        _eventStorage = eventStorage;
        _handlerRunnerChannel = handlerRunnerChannel;
        _cancellationTokenSource = cancellationTokenSource;
        _pollRequiredSignal = new PollRequiredSignal();
        _logger = logger;
    }

    public void Run()
    {
        _ = Task.Factory.StartNew(PollEventStorageAsync, TaskCreationOptions.LongRunning | TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private async Task PollEventStorageAsync()
    {
        var cancellationToken = _cancellationTokenSource.Token;
        try
        {
            while(!cancellationToken.IsCancellationRequested)
            {
                _pollRequiredSignal.Reset();
                IEnumerable<EventRecord> eventRecords =
                    await _eventStorage.TryFetchScheduledAsync(_settings.ScheduledBatchSize, _logger, cancellationToken).ConfigureAwait(false);
                if(eventRecords.Any())
                {
                    foreach(EventRecord eventRecord in eventRecords)
                    {
                        await _handlerRunnerChannel.Writer.WriteAsync(eventRecord, cancellationToken).ConfigureAwait(false);
                    }
                }
                else
                {
                    await _pollRequiredSignal.WaitForSignalAsync(_settings.PollingInterval, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "An error occurred while polling event storage.");
        }
    }
}
