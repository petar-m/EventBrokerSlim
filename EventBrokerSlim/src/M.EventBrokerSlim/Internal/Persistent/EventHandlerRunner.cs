using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using FuncPipeline;
using M.EventBrokerSlim.DependencyInjection;
using M.EventBrokerSlim.Internal.ObjectPools;
using M.EventBrokerSlim.Persistent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace M.EventBrokerSlim.Internal.Persistent;

internal sealed class EventHandlerRunner
{
    private readonly ChannelReader<ScheduledEventRecord> _channelReader;
    private readonly PipelineRegistry _pipelineRegistry;
    private readonly EventRegistry _eventNameRegistry;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly ILogger _logger;
    private readonly EventBrokerSettings _settings;
    private readonly IEventStorage _eventStorage;
    private readonly SemaphoreSlim _semaphore;
    private DefaultObjectPool<HandlerExecutionContext> _executionContextObjectPool;
    private DefaultObjectPool<PipelineRunContext> _pipelineRunContextObjectPool;
    private DefaultObjectPool<RetryPolicy> _retryPolicyObjectPool;

    internal EventHandlerRunner(
        ChannelReader<ScheduledEventRecord> channelReader,
        PipelineRegistry pipelineRegistry,
        EventRegistry eventNameRegistry,
        CancellationTokenSource cancellationTokenSource,
        ILogger logger,
        EventBrokerSettings settings,
        IEventStorage eventStorage)
    {
        _channelReader = channelReader;
        _pipelineRegistry = pipelineRegistry;
        _eventNameRegistry = eventNameRegistry;
        _cancellationTokenSource = cancellationTokenSource;
        _logger = logger;
        _settings = settings;
        _eventStorage = eventStorage;
        _semaphore = new SemaphoreSlim(_settings.MaxConcurrentHandlers, _settings.MaxConcurrentHandlers);
        _executionContextObjectPool = new DefaultObjectPool<HandlerExecutionContext>(new HandlerExecutionContext.ObjectPoolPolicy(), _settings.MaxConcurrentHandlers);
        _pipelineRunContextObjectPool = new DefaultObjectPool<PipelineRunContext>(new PipelineRunContextPooledObjectPolicy(), _settings.MaxConcurrentHandlers);
        _retryPolicyObjectPool = new DefaultObjectPool<RetryPolicy>(new RetryPolicyPooledObjectPolicy(), _settings.MaxConcurrentHandlers);
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
            while(_channelReader.TryRead(out ScheduledEventRecord? scheduledEventRecord))
            {
                if(scheduledEventRecord is null)
                {
                    continue;
                }

                Type? eventType = _eventNameRegistry.GetEventType(scheduledEventRecord.EventName);
                if(eventType is null)
                {
                    continue;
                }

                EventPipeline? handler = _pipelineRegistry.Get(scheduledEventRecord.HandlerName);
                if(handler is null)
                {
                    continue;
                }

                await _semaphore.WaitAsync(token).ConfigureAwait(false);
                IPipeline pipeline = handler.Pipeline;
                HandlerExecutionContext context = _executionContextObjectPool.Get();
                context.Initialize(
                    scheduledEventRecord,
                    pipeline,
                    eventType,
                    token,
                    _executionContextObjectPool,
                    _pipelineRunContextObjectPool,
                    _retryPolicyObjectPool,
                    _logger,
                    _semaphore,
                    _eventStorage,
                    _eventNameRegistry);
                _ = Task.Factory.StartNew(static async x => await HandleEventWithDelegate(x!).ConfigureAwait(false), context);
            }
        }
    }

    private static async Task HandleEventWithDelegate(object state)
    {
        var context = (HandlerExecutionContext)state;
        ScheduledEventRecord scheduledEventRecord = context.ScheduledEventRecord;
        Type eventType = context.EventType;
        IPipeline pipeline = context.Pipeline;
        IEventStorage eventStorage = context.EventStorage;
        ILogger logger = context.Logger;
        DefaultObjectPool<RetryPolicy> retryPolicyObjectPool = context.RetryPolicyObjectPool;
        DefaultObjectPool<PipelineRunContext> pipelineRunContextObjectPool = context.PipelineRunContextObjectPool;
        CancellationToken cancellationToken = context.CancellationToken;
        DefaultObjectPool<HandlerExecutionContext> contextObjectPool = context.ObjectPool;
        SemaphoreSlim semaphore = context.Semaphore;
        EventRegistry eventRegistry = context.EventRegistry;

        RetryPolicy retryPolicy = retryPolicyObjectPool.Get();
        PipelineRunContext pipelineRunContext = pipelineRunContextObjectPool.Get();
        PipelineRunResult? result = null;
        try
        {
            if(cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var eventRecord = await eventStorage.TryClaimAsync(scheduledEventRecord, eventRegistry ,logger, cancellationToken).ConfigureAwait(false);
            if(eventRecord == EventRecord.Empty)
            {
                return;
            }

            retryPolicy.Attempt = (uint)eventRecord.RetryAttemptCount;
            retryPolicy.LastDelay = eventRecord.RetryLastDelay;

            pipelineRunContext
                .Set(eventType, eventRecord.DeserializedEvent)
                .Set<IRetryPolicy>(retryPolicy)
                .Set<CancellationToken>(cancellationToken)
                .Set<EventRecord>(eventRecord);

            result = await pipeline.RunAsync(pipelineRunContext, cancellationToken).ConfigureAwait(false);
            if(result.Exception is not null)
            {
                logger.LogError(result.Exception, "An error occurred while processing event record with id {EventRecordId} and event name {EventName}", scheduledEventRecord.Id, scheduledEventRecord.EventName);
                await eventStorage.TryDeadLetterAsync(scheduledEventRecord.Id, "Processing failed", logger, cancellationToken).ConfigureAwait(false);
                return;
            }

            if(retryPolicy.RetryRequested)
            {
                await eventStorage.TryRetryAsync(scheduledEventRecord.Id, (int)retryPolicy.Attempt + 1, retryPolicy.LastDelay, logger, cancellationToken).ConfigureAwait(false);
            }
            else if(retryPolicy.Abandoned)
            {
                await eventStorage.TryDeadLetterAsync(scheduledEventRecord.Id, "Abandoned by retry policy", logger, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await eventStorage.TryCompleteAsync(scheduledEventRecord.Id, logger, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            retryPolicyObjectPool.Return(retryPolicy);
            pipelineRunContextObjectPool.Return(pipelineRunContext);
            contextObjectPool.Return(context);
            semaphore.Release();
        }
    }
}
