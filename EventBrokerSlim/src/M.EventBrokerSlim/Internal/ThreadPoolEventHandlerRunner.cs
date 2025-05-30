﻿using System;
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

namespace M.EventBrokerSlim.Internal;

internal sealed class ThreadPoolEventHandlerRunner
{
    private readonly ChannelReader<object> _channelReader;
    private readonly PipelineRegistry _pipelineRegistry;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly ILogger _logger;
    private readonly DynamicEventHandlers _dynamicEventHandlers;
    private readonly EventBrokerSettings _settings;
    private readonly SemaphoreSlim _semaphore;
    private DefaultObjectPool<HandlerExecutionContext> _handlerExecutionContextObjectPool;

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
        
        var retryQueue = new RetryQueue(channel.Writer, _cancellationTokenSource.Token);
        var policy = new HandlerExecutionContextPooledObjectPolicy(_semaphore, _logger, retryQueue, _settings.MaxConcurrentHandlers);
        _handlerExecutionContextObjectPool = new DefaultObjectPool<HandlerExecutionContext>(policy, _settings.MaxConcurrentHandlers);
        policy.HandlerExecutionContextObjectPool = _handlerExecutionContextObjectPool;
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
                    ImmutableArray<IPipeline> handlers = _pipelineRegistry.Get(type);
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

                        IPipeline pipeline = handlers[i];

                        HandlerExecutionContext context = _handlerExecutionContextObjectPool.Get();
                        context.Initialize(@event, pipeline, retryDescriptor, token);
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

                        HandlerExecutionContext context = _handlerExecutionContextObjectPool.Get();
                        context.Initialize(@event, pipeline, retryDescriptor, token);
                        _ = Task.Factory.StartNew(static async x => await HandleEventWithDelegate(x!).ConfigureAwait(false), context);
                    }
                }
                else
                {
                    await _semaphore.WaitAsync(token).ConfigureAwait(false);

                    HandlerExecutionContext context = _handlerExecutionContextObjectPool.Get();
                    context.Initialize(retryDescriptor.Event, retryDescriptor.Pipeline, retryDescriptor, token);
                    _ = Task.Factory.StartNew(static async x => await HandleEventWithDelegate(x!).ConfigureAwait(false), context);
                }
            }
        }
    }

    private static async Task HandleEventWithDelegate(object state)
    {
        var context = (HandlerExecutionContext)state;
        var @event = context.Event!;
        var pipeline = context.Pipeline!;

        if(context.CancellationToken.IsCancellationRequested)
        {
            return;
        }

        RetryPolicy retryPolicy = context.RetryDescriptor?.RetryPolicy ?? context.RetryPolicyObjectPool.Get();

        PipelineRunContext pipelineRunContext = context.PipelineRunContextObjectPool.Get();
        pipelineRunContext
            .Set(@event.GetType(), @event)
            .Set<IRetryPolicy>(retryPolicy)
            .Set<CancellationToken>(context.CancellationToken);
        try
        {
            var result = await pipeline.RunAsync(pipelineRunContext, context.CancellationToken).ConfigureAwait(false);
            if(result.Exception is not null)
            {
                context.Logger?.LogDelegateEventHandlerError(@event.GetType(), result.Exception);
            }
        }
        finally
        {
            if(retryPolicy.RetryRequested)
            {
                retryPolicy.NextAttempt();
                var retryDescriptor = context.RetryDescriptor ?? new RetryDescriptor(@event, retryPolicy, pipeline);
                await context.RetryQueue.Enqueue(retryDescriptor).ConfigureAwait(false);
            }
            else
            {
                context.RetryPolicyObjectPool.Return(retryPolicy);
            }

            context.PipelineRunContextObjectPool.Return(pipelineRunContext);
            context.HandlerExecutionContextObjectPool.Return(context);
            context.Semaphore.Release();
        }
    }
}
