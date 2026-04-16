using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using M.EventBrokerSlim.DependencyInjection;
using M.EventBrokerSlim.Persistent;
using Microsoft.Extensions.Logging;

namespace M.EventBrokerSlim.Internal.Persistent;

internal class PersistentEventBroker : IEventBroker
{
    private readonly IEventStorage _storage;
    private readonly EventRegistry _eventNameRegistry;
    private readonly PipelineRegistry _pipelineRegistry;
    private readonly PollRequiredSignal _pollRequiredSignal;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly EventBrokerSettings _settings;
    private readonly ILogger _logger;

    internal PersistentEventBroker(
        IEventStorage storage, 
        EventRegistry eventNameRegistry, 
        PipelineRegistry pipelineRegistry, 
        PollRequiredSignal pollRequiredSignal,
        CancellationTokenSource cancellationTokenSource,
        EventBrokerSettings settings,
        ILogger logger)
    {
        _storage = storage;
        _eventNameRegistry = eventNameRegistry;
        _pipelineRegistry = pipelineRegistry;
        _pollRequiredSignal = pollRequiredSignal;
        _cancellationTokenSource = cancellationTokenSource;
        _settings = settings;
        _logger = logger;
    }

    public async Task Publish<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : notnull
    {
        string? eventName = _eventNameRegistry.GetEventName<TEvent>();
        if(eventName is null)
        {
            if(!_settings.DisableMissingHandlerWarningLog)
            {
                _logger.LogNoEventHandlerForEvent(typeof(TEvent));
            }
            return;
        }

        ImmutableArray<string> handlerNames = _pipelineRegistry.GetHandlerNames<TEvent>();
        if (!handlerNames.IsEmpty)
        {
            await _storage.ScheduleAsync(@event, eventName, handlerNames, cancellationToken);
            _pollRequiredSignal.Send();
        }
    }

    public async Task PublishDeferred<TEvent>(TEvent @event, TimeSpan deferDuration) where TEvent : notnull
    {
        string? eventName = _eventNameRegistry.GetEventName<TEvent>();
        if(eventName is null)
        {
            if(!_settings.DisableMissingHandlerWarningLog)
            {
                _logger.LogNoEventHandlerForEvent(typeof(TEvent));
            }
            return;
        }

        ImmutableArray<string> handlerNames = _pipelineRegistry.GetHandlerNames<TEvent>();
        if(!handlerNames.IsEmpty)
        {
            await _storage.ScheduleDeferredAsync(@event, deferDuration, eventName, handlerNames, _cancellationTokenSource.Token);
        }
    }

    public void Shutdown()
    {
        _cancellationTokenSource.Cancel();
    }
}
