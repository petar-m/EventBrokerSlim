using System;
using System.Threading;
using FuncPipeline;
using M.EventBrokerSlim.Internal.ObjectPools;
using M.EventBrokerSlim.Persistent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace M.EventBrokerSlim.Internal.Persistent;

internal sealed class HandlerExecutionContext : IResettable
{
    public HandlerExecutionContext Initialize(EventRecord eventRecord, IPipeline pipeline, Type eventType, CancellationToken cancellationToken)
    {
        EventRecord = eventRecord;
        Pipeline = pipeline;
        EventType = eventType;
        CancellationToken = cancellationToken;
        return this;
    }

    public bool TryReset()
    {
        EventRecord = null;
        Pipeline = null;
        EventType = null;
        CancellationToken = default;
        return true;
    }

    public EventRecord? EventRecord { get; private set; }

    public IPipeline? Pipeline { get; private set; }

    public Type? EventType { get; private set; }

    public CancellationToken CancellationToken { get; private set; }

    public static SemaphoreSlim? Semaphore;

    public static ILogger? Logger;
    public static IEventStorage? EventStorage { get; internal set; }
    public static DefaultObjectPool<HandlerExecutionContext> ObjectPool => _lazyObjectPool!.Value;
    public static DefaultObjectPool<PipelineRunContext> PipelineRunContextObjectPool => _lazyPipelineRunContextObjectPool!.Value;
    public static DefaultObjectPool<RetryPolicy> RetryPolicyObjectPool => _lazyRetryPolicyObjectPool!.Value;

    public static void ConfigureObjectPools(int maxRetained)
    {
        _lazyObjectPool = new Lazy<DefaultObjectPool<HandlerExecutionContext>>(() => new DefaultObjectPool<HandlerExecutionContext>(new ObjectPoolPolicy(), maxRetained));
        _lazyPipelineRunContextObjectPool = new Lazy<DefaultObjectPool<PipelineRunContext>>(() => new DefaultObjectPool<PipelineRunContext>(new PipelineRunContextPooledObjectPolicy(), maxRetained));
        _lazyRetryPolicyObjectPool = new Lazy<DefaultObjectPool<RetryPolicy>>(() => new DefaultObjectPool<RetryPolicy>(new RetryPolicyPooledObjectPolicy(), maxRetained));
    }

    private static Lazy<DefaultObjectPool<HandlerExecutionContext>>? _lazyObjectPool;
    private static Lazy<DefaultObjectPool<PipelineRunContext>>? _lazyPipelineRunContextObjectPool;
    private static Lazy<DefaultObjectPool<RetryPolicy>>? _lazyRetryPolicyObjectPool;

    private class ObjectPoolPolicy : IPooledObjectPolicy<HandlerExecutionContext>
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
