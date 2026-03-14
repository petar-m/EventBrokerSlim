using System;
using System.Threading;
using FuncPipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace M.EventBrokerSlim.Internal;

internal sealed class HandlerExecutionContext
{
    public HandlerExecutionContext(
        SemaphoreSlim semaphore,
        RetryQueue retryQueue,
        DefaultObjectPool<PipelineRunContext> pipelineRunContextObjectPool,
        DefaultObjectPool<HandlerExecutionContext>? handlerExecutionContextObjectPool)
    {
        Semaphore = semaphore;
        RetryQueue = retryQueue;
        PipelineRunContextObjectPool = pipelineRunContextObjectPool;
        HandlerExecutionContextObjectPool = handlerExecutionContextObjectPool ?? throw new ArgumentNullException(nameof(handlerExecutionContextObjectPool));
    }

    public HandlerExecutionContext Initialize(object @event, IPipeline pipeline, RetryDescriptor? retryDescriptor, CancellationToken cancellationToken)
    {
        Event = @event;
        Pipeline = pipeline;
        RetryDescriptor = retryDescriptor;
        CancellationToken = cancellationToken;
        RetryPolicy = RetryDescriptor?.RetryPolicy;
        return this;
    }

    public void Clear()
    {
        Event = null;
        Pipeline = null;
        RetryDescriptor = null;
        CancellationToken = default;
        RetryPolicy = null;
    }

    public object? Event { get; private set; }

    public IPipeline? Pipeline { get; private set; }

    public RetryDescriptor? RetryDescriptor { get; private set; }

    public CancellationToken CancellationToken { get; private set; }

    public RetryPolicy? RetryPolicy { get; private set; }

    public SemaphoreSlim Semaphore { get; }
    public static ILogger? Logger;
    public RetryQueue RetryQueue { get; }

    public DefaultObjectPool<PipelineRunContext> PipelineRunContextObjectPool { get; }
    public DefaultObjectPool<HandlerExecutionContext> HandlerExecutionContextObjectPool { get; }
}
