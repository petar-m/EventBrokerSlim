using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using FuncPipeline;
using M.EventBrokerSlim.Persistent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace M.EventBrokerSlim.Internal.Persistent;

internal sealed class HandlerExecutionContext : IResettable
{
    public HandlerExecutionContext Initialize(
        EventRecord eventRecord,
        IPipeline pipeline,
        Type eventType,
        CancellationToken cancellationToken,
        DefaultObjectPool<HandlerExecutionContext> objectPool,
        DefaultObjectPool<PipelineRunContext> pipelineRunContextObjectPool,
        DefaultObjectPool<RetryPolicy> retryPolicyObjectPool,
        ILogger logger,
        SemaphoreSlim semaphore,
        IEventStorage eventStorage)
    {
        EventRecord = eventRecord;
        Pipeline = pipeline;
        EventType = eventType;
        CancellationToken = cancellationToken;
        ObjectPool = objectPool;
        PipelineRunContextObjectPool = pipelineRunContextObjectPool;
        RetryPolicyObjectPool = retryPolicyObjectPool;
        Logger = logger;
        Semaphore = semaphore;
        EventStorage = eventStorage;
        return this;
    }

    public bool TryReset()
    {
        EventRecord = null;
        Pipeline = null;
        EventType = null;
        CancellationToken = default;
        ObjectPool = null;
        PipelineRunContextObjectPool = null;
        RetryPolicyObjectPool = null;
        Logger = null;
        Semaphore = null;
        EventStorage = null;
        return true;
    }

    [NotNull] public EventRecord? EventRecord { get; private set; }
    [NotNull] public IPipeline? Pipeline { get; private set; }
    [NotNull] public Type? EventType { get; private set; }
    [NotNull] public CancellationToken CancellationToken { get; private set; }
    [NotNull] public DefaultObjectPool<HandlerExecutionContext>? ObjectPool { get; private set; }
    [NotNull] public DefaultObjectPool<PipelineRunContext>? PipelineRunContextObjectPool { get; private set; }
    [NotNull] public DefaultObjectPool<RetryPolicy>? RetryPolicyObjectPool { get; private set; }
    [NotNull] public SemaphoreSlim? Semaphore { get; private set; }
    [NotNull] public ILogger? Logger { get; private set; }
    [NotNull] public IEventStorage? EventStorage { get; internal set; }

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
