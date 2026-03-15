using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using FuncPipeline;
using M.EventBrokerSlim.DependencyInjection;
using M.EventBrokerSlim.Persistent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace M.EventBrokerSlim.Internal.Persistent;

internal sealed class EventHandlerRunner
{
    private readonly ChannelReader<EventRecord> _channelReader;
    private readonly PipelineRegistry _pipelineRegistry;
    private readonly EventRegistry _eventNameRegistry;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly ILogger _logger;
    private readonly EventBrokerSettings _settings;
    private readonly IEventStorage _eventStorage;
    private readonly SemaphoreSlim _semaphore;

    internal EventHandlerRunner(
        ChannelReader<EventRecord> channelReader,
        IServiceScopeFactory serviceScopeFactory,
        PipelineRegistry pipelineRegistry,
        EventRegistry eventNameRegistry,
        CancellationTokenSource cancellationTokenSource,
        ILogger? logger,
        EventBrokerSettings settings,
        IEventStorage eventStorage)
    {
        _channelReader = channelReader;
        _pipelineRegistry = pipelineRegistry;
        _eventNameRegistry = eventNameRegistry;
        _cancellationTokenSource = cancellationTokenSource;
        _logger = logger ?? new NullLogger<EventHandlerRunner>();
        _settings = settings;
        _eventStorage = eventStorage;
        _semaphore = new SemaphoreSlim(_settings.MaxConcurrentHandlers, _settings.MaxConcurrentHandlers);

        HandlerExecutionContext.Semaphore = _semaphore;
        HandlerExecutionContext.Logger = _logger;
        HandlerExecutionContext.EventStorage = _eventStorage;
        HandlerExecutionContext.ConfigureObjectPools(_settings.MaxConcurrentHandlers);
    }

    public void Run()
    {
        _ = Task.Factory.StartNew(ProcessEvents, TaskCreationOptions.LongRunning | TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private async ValueTask ProcessEvents()
    {
        CancellationToken token = _cancellationTokenSource.Token;
        while(await _channelReader.WaitToReadAsync(token).ConfigureAwait(false))
        {
            while(_channelReader.TryRead(out EventRecord? eventRecord))
            {
                if(eventRecord is null)
                {
                    continue;
                }

                Type? eventType = _eventNameRegistry.GetEventType(eventRecord.EventName);
                if(eventType is null)
                {
                    continue;
                }

                EventPipeline? handler = _pipelineRegistry.Get(eventRecord.HandlerName);
                if(handler is null)
                {
                    continue;
                }

                await _semaphore.WaitAsync(token).ConfigureAwait(false);
                IPipeline pipeline = handler.Pipeline;
                HandlerExecutionContext context = HandlerExecutionContext.ObjectPool.Get();
                context.Initialize(eventRecord, pipeline, eventType, token);
                _ = Task.Factory.StartNew(static async x => await HandleEventWithDelegate(x!).ConfigureAwait(false), context);
            }
        }
    }

    private static async Task HandleEventWithDelegate(object state)
    {
        var context = (HandlerExecutionContext)state;
        var eventRecord = context.EventRecord!;
        var eventType = context.EventType!;
        var pipeline = context.Pipeline!;
        var eventStorage = HandlerExecutionContext.EventStorage!;
        var logger = HandlerExecutionContext.Logger!;

        if(context.CancellationToken.IsCancellationRequested)
        {
            return;
        }

        var claimed = await eventStorage.TryClaimAsync(eventRecord.Id, logger, context.CancellationToken).ConfigureAwait(false);
        if(!claimed)
        {
            return;
        }

        RetryPolicy retryPolicy = HandlerExecutionContext.RetryPolicyObjectPool.Get();
        PipelineRunContext pipelineRunContext = HandlerExecutionContext.PipelineRunContextObjectPool.Get();
        PipelineRunResult? result = null;
        try
        {
            retryPolicy.Attempt = (uint)eventRecord.RetryAttemptCount;
            retryPolicy.LastDelay = eventRecord.RetryLastDelay;

            pipelineRunContext
                .Set(eventType, eventRecord.DeserializedEvent)
                .Set<IRetryPolicy>(retryPolicy)
                .Set<CancellationToken>(context.CancellationToken)
                .Set<EventRecord>(eventRecord);

            result = await pipeline.RunAsync(pipelineRunContext, context.CancellationToken).ConfigureAwait(false);
            if(result.Exception is not null)
            {
                logger.LogError(result.Exception, "An error occurred while processing event record with id {EventRecordId} and event name {EventName}", eventRecord.Id, eventRecord.EventName);
                await eventStorage.TryDeadLetterAsync(eventRecord.Id, "Processing failed", logger, context.CancellationToken).ConfigureAwait(false);
                return;
            }

            if(retryPolicy.RetryRequested)
            {
                await eventStorage.TryRetryAsync(eventRecord.Id, (int)retryPolicy.Attempt + 1, retryPolicy.LastDelay, logger, context.CancellationToken).ConfigureAwait(false);
            }
            else if(retryPolicy.Abandoned)
            {
                await eventStorage.TryDeadLetterAsync(eventRecord.Id, "Abandoned by retry policy", logger, context.CancellationToken).ConfigureAwait(false);
            }
            else
            {
                await eventStorage.TryCompleteAsync(eventRecord.Id, logger, context.CancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            HandlerExecutionContext.RetryPolicyObjectPool.Return(retryPolicy);
            HandlerExecutionContext.PipelineRunContextObjectPool.Return(pipelineRunContext);
            HandlerExecutionContext.ObjectPool.Return(context);

            HandlerExecutionContext.Semaphore!.Release();
        }
    }
}
