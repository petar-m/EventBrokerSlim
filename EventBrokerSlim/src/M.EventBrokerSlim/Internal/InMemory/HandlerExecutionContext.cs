using System.Diagnostics.CodeAnalysis;
using System.Threading;
using FuncPipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace M.EventBrokerSlim.Internal.InMemory;

internal sealed class HandlerExecutionContext : IResettable
{
    public HandlerExecutionContext Initialize(
        object @event,
        IPipeline pipeline,
        RetryDescriptor? retryDescriptor,
        CancellationToken cancellationToken,
        SemaphoreSlim semaphore,
        ILogger logger,
        RetryQueue retryQueue,
        DefaultObjectPool<PipelineRunContext> pipelineRunContextObjectPool,
        DefaultObjectPool<RetryPolicy> retryPolicyObjectPool,
        DefaultObjectPool<HandlerExecutionContext> objectPool)
    {
        Event = @event;
        Pipeline = pipeline;
        RetryDescriptor = retryDescriptor;
        CancellationToken = cancellationToken;
        Semaphore = semaphore;
        Logger = logger;
        RetryQueue = retryQueue;
        PipelineRunContextObjectPool = pipelineRunContextObjectPool;
        RetryPolicyObjectPool = retryPolicyObjectPool;
        ObjectPool = objectPool;
        return this;
    }

    public bool TryReset()
    {
        Event = null;
        Pipeline = null;
        RetryDescriptor = null;
        CancellationToken = default;
        Semaphore = null;
        Logger = null;
        RetryQueue = null;
        PipelineRunContextObjectPool = null;
        RetryPolicyObjectPool = null;
        ObjectPool = null;
        return true;
    }

    [NotNull] public object? Event { get; private set; }
    [NotNull] public IPipeline? Pipeline { get; private set; }
    public RetryDescriptor? RetryDescriptor { get; private set; }
    [NotNull] public CancellationToken CancellationToken { get; private set; }
    [NotNull] public SemaphoreSlim? Semaphore { get; private set; }
    [NotNull] public ILogger? Logger { get; private set; }
    [NotNull] public RetryQueue? RetryQueue { get; private set; }
    [NotNull] public DefaultObjectPool<PipelineRunContext>? PipelineRunContextObjectPool { get; private set; }
    [NotNull] public DefaultObjectPool<RetryPolicy>? RetryPolicyObjectPool { get; private set; }
    [NotNull] public DefaultObjectPool<HandlerExecutionContext>? ObjectPool { get; private set; }

    internal class ObjectPoolPolicy : IPooledObjectPolicy<HandlerExecutionContext>
    {
        public HandlerExecutionContext Create()
        {
            return new HandlerExecutionContext();
        }

        public bool Return(HandlerExecutionContext obj)
        {
            obj.TryReset();
            return true;
        }
    }
}
