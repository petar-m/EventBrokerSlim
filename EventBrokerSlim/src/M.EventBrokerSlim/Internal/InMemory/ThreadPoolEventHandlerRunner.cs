using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using FuncPipeline;
using M.EventBrokerSlim.DependencyInjection;
using M.EventBrokerSlim.Internal.ObjectPools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.ObjectPool;

namespace M.EventBrokerSlim.Internal.InMemory;

internal sealed class ThreadPoolEventHandlerRunner
{
    private readonly ChannelReader<object> _channelReader;
    private readonly PipelineRegistry _pipelineRegistry;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly ILogger _logger;
    private readonly DynamicEventHandlers _dynamicEventHandlers;
    private readonly EventBrokerSettings _settings;
    private readonly SemaphoreSlim _semaphore;
    private readonly DefaultObjectPool<HandlerExecutionContext> _executionContextObjectPool;
    private readonly DefaultObjectPool<PipelineRunContext> _pipelineRunContextObjectPool;
    private readonly DefaultObjectPool<RetryPolicy> _retryPolicyObjectPool;
    private readonly RetryQueue _retryQueue;

    internal ThreadPoolEventHandlerRunner(
        Channel<object> channel,
        IServiceScopeFactory serviceScopeFactory,
        PipelineRegistry pipelineRegistry,
        CancellationTokenSource cancellationTokenSource,
        ILogger? logger,
        DynamicEventHandlers dynamicEventHandlers,
        EventBrokerSettings settings)
    {
        _channelReader = channel.Reader;
        _pipelineRegistry = pipelineRegistry;
        _cancellationTokenSource = cancellationTokenSource;
        _logger = logger ?? new NullLogger<ThreadPoolEventHandlerRunner>();
        _dynamicEventHandlers = dynamicEventHandlers;
        _settings = settings;
        _semaphore = new SemaphoreSlim(_settings.MaxConcurrentHandlers, _settings.MaxConcurrentHandlers);
        _executionContextObjectPool = new DefaultObjectPool<HandlerExecutionContext>(new HandlerExecutionContext.ObjectPoolPolicy(), _settings.MaxConcurrentHandlers);
        _pipelineRunContextObjectPool = new DefaultObjectPool<PipelineRunContext>(new PipelineRunContextPooledObjectPolicy(), _settings.MaxConcurrentHandlers);
        _retryPolicyObjectPool = new DefaultObjectPool<RetryPolicy>(new RetryPolicyPooledObjectPolicy(), _settings.MaxConcurrentHandlers);
        _retryQueue = new RetryQueue(channel.Writer, _cancellationTokenSource.Token);
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
            while(_channelReader.TryRead(out var @event))
            {
                RetryDescriptor? retryDescriptor = @event as RetryDescriptor;
                if(retryDescriptor is null)
                {
                    Type type = @event.GetType();
                    ImmutableArray<EventPipeline> handlers = _pipelineRegistry.Get(type);
                    ImmutableList<(DynamicHandlerClaimTicket ticket, IPipeline pipeline)>? dynamicEventHandlers = _dynamicEventHandlers.GetDelegateHandlerDescriptors(type);

                    if(handlers.Length == 0 && (dynamicEventHandlers?.IsEmpty ?? true))
                    {
                        if(!_settings.DisableMissingHandlerWarningLog)
                        {
                            _logger.LogNoEventHandlerForEvent(type);
                        }

                        continue;
                    }

                    for(int i = 0; i < handlers.Length; i++)
                    {
                        await _semaphore.WaitAsync(token).ConfigureAwait(false);

                        IPipeline pipeline = handlers[i].Pipeline;

                        HandlerExecutionContext context = _executionContextObjectPool.Get();
                        context.Initialize(@event, pipeline, retryDescriptor, token, _semaphore, _logger, _retryQueue, _pipelineRunContextObjectPool, _retryPolicyObjectPool, _executionContextObjectPool);
                        _ = Task.Factory.StartNew(static async x => await HandleEventWithDelegate(x!).ConfigureAwait(false), context);
                    }

                    if(dynamicEventHandlers is null || dynamicEventHandlers.IsEmpty)
                    {
                        continue;
                    }

                    for(int i = 0; i < dynamicEventHandlers.Count; i++)
                    {
                        await _semaphore.WaitAsync(token).ConfigureAwait(false);

                        IPipeline pipeline = dynamicEventHandlers[i].pipeline;

                        HandlerExecutionContext context = _executionContextObjectPool.Get();
                        context.Initialize(@event, pipeline, retryDescriptor, token, _semaphore, _logger, _retryQueue, _pipelineRunContextObjectPool, _retryPolicyObjectPool, _executionContextObjectPool);
                        _ = Task.Factory.StartNew(static async x => await HandleEventWithDelegate(x!).ConfigureAwait(false), context);
                    }
                }
                else
                {
                    await _semaphore.WaitAsync(token).ConfigureAwait(false);

                    HandlerExecutionContext context = _executionContextObjectPool.Get();
                    context.Initialize(retryDescriptor.Event, retryDescriptor.Pipeline, retryDescriptor, token, _semaphore, _logger, _retryQueue, _pipelineRunContextObjectPool, _retryPolicyObjectPool, _executionContextObjectPool);
                    _ = Task.Factory.StartNew(static async x => await HandleEventWithDelegate(x!).ConfigureAwait(false), context);
                }
            }
        }
    }

    private static async Task HandleEventWithDelegate(object state)
    {
        var context = (HandlerExecutionContext)state;
        object @event = context.Event!;
        IPipeline pipeline = context.Pipeline!;
        RetryDescriptor? retryDescriptor = context.RetryDescriptor;
        DefaultObjectPool<RetryPolicy> retryPolicyObjectPool = context.RetryPolicyObjectPool;
        DefaultObjectPool<PipelineRunContext> pipelineRunContextObjectPool = context.PipelineRunContextObjectPool;
        DefaultObjectPool<HandlerExecutionContext> objectPool = context.ObjectPool;
        ILogger logger = context.Logger;
        RetryQueue retryQueue = context.RetryQueue;
        SemaphoreSlim semaphore = context.Semaphore;
        CancellationToken cancellationToken = context.CancellationToken;

        if(cancellationToken.IsCancellationRequested)
        {
            return;
        }

        RetryPolicy retryPolicy = retryDescriptor?.RetryPolicy ?? retryPolicyObjectPool.Get();
        PipelineRunContext pipelineRunContext = pipelineRunContextObjectPool.Get();
        pipelineRunContext
            .Set(@event.GetType(), @event)
            .Set<IRetryPolicy>(retryPolicy)
            .Set<CancellationToken>(cancellationToken);
        try
        {
            var result = await pipeline.RunAsync(pipelineRunContext, cancellationToken).ConfigureAwait(false);
            if(result.Exception is not null)
            {
                logger.LogDelegateEventHandlerError(@event.GetType(), result.Exception);
            }
        }
        finally
        {
            if(retryPolicy.RetryRequested)
            {
                retryPolicy.NextAttempt();
                retryDescriptor ??= new RetryDescriptor(@event, retryPolicy, pipeline);
                await retryQueue.Enqueue(retryDescriptor).ConfigureAwait(false);
            }
            else
            {
                retryPolicyObjectPool.Return(retryPolicy);
            }

            pipelineRunContextObjectPool.Return(pipelineRunContext);
            objectPool.Return(context);
            semaphore.Release();
        }
    }
}
