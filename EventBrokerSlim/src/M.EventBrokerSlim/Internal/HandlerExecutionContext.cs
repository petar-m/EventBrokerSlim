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
        ILogger logger,
        RetryQueue retryQueue,
        DefaultObjectPool<RetryPolicy> retryPolicyObjectPool,
        DefaultObjectPool<PipelineRunContext> pipelineRunContextObjectPool,
        DefaultObjectPool<HandlerExecutionContext>? handlerExecutionContextObjectPool)
    {
        Semaphore = semaphore;
        Logger = logger;
        RetryQueue = retryQueue;
        RetryPolicyObjectPool = retryPolicyObjectPool;
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
    public ILogger Logger { get; }
    public RetryQueue RetryQueue { get; }
    public DefaultObjectPool<RetryPolicy> RetryPolicyObjectPool { get; }
    public DefaultObjectPool<PipelineRunContext> PipelineRunContextObjectPool { get; }
    public DefaultObjectPool<HandlerExecutionContext> HandlerExecutionContextObjectPool { get; }
}
