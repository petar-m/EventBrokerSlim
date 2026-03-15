using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using M.EventBrokerSlim.DependencyInjection;
using M.EventBrokerSlim.Persistent;

namespace M.EventBrokerSlim.Internal.Persistent;

internal class PersistentEventBroker : IEventBroker
{
    private readonly IEventStorage _storage;
    private readonly EventRegistry _eventNameRegistry;
    private readonly PipelineRegistry _pipelineRegistry;
    private readonly PollRequiredSignal _pollRequiredSignal;

    internal PersistentEventBroker(IEventStorage storage, EventRegistry eventNameRegistry, PipelineRegistry pipelineRegistry, PollRequiredSignal pollRequiredSignal)
    {
        _storage = storage;
        _eventNameRegistry = eventNameRegistry;
        _pipelineRegistry = pipelineRegistry;
        _pollRequiredSignal = pollRequiredSignal;
    }

    public async Task Publish<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : notnull
    {
        string eventName = _eventNameRegistry.GetEventName<TEvent>() ?? throw new InvalidOperationException($"Event name for type {typeof(TEvent).Name} is not registered.");
        ImmutableArray<string> handlerNames = _pipelineRegistry.GetHandlerNames<TEvent>();
        if (!handlerNames.IsEmpty)
        {
            await _storage.ScheduleAsync(@event, eventName, handlerNames, cancellationToken);
            _pollRequiredSignal.Send();
        }
    }

    public async Task PublishDeferred<TEvent>(TEvent @event, TimeSpan deferDuration) where TEvent : notnull
    {
        string eventName = _eventNameRegistry.GetEventName<TEvent>() ?? throw new InvalidOperationException($"Event name for type {typeof(TEvent).Name} is not registered.");
        ImmutableArray<string> handlerNames = _pipelineRegistry.GetHandlerNames<TEvent>();
        if(!handlerNames.IsEmpty)
        {
            await _storage.ScheduleDeferredAsync(@event, deferDuration, eventName, handlerNames, default);
        }
    }

    public void Shutdown()
    {
    }
}
