using System;
using System.Threading;
using FuncPipeline;
using M.EventBrokerSlim.Internal.ObjectPools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace M.EventBrokerSlim.Internal;

internal sealed class HandlerExecutionContext : IResettable
{
    public HandlerExecutionContext Initialize(object @event, IPipeline pipeline, RetryDescriptor? retryDescriptor, CancellationToken cancellationToken)
    {
        Event = @event;
        Pipeline = pipeline;
        RetryDescriptor = retryDescriptor;
        CancellationToken = cancellationToken;
        RetryPolicy = RetryDescriptor?.RetryPolicy;
        return this;
    }

    public bool TryReset()
    {
        Event = null;
        Pipeline = null;
        RetryDescriptor = null;
        CancellationToken = default;
        RetryPolicy = null;
        return true;
    }

    public object? Event { get; private set; }

    public IPipeline? Pipeline { get; private set; }

    public RetryDescriptor? RetryDescriptor { get; private set; }

    public CancellationToken CancellationToken { get; private set; }

    public RetryPolicy? RetryPolicy { get; private set; }

    public static SemaphoreSlim? Semaphore;
    public static ILogger? Logger;
    public static RetryQueue? RetryQueue;
    
    public static DefaultObjectPool<PipelineRunContext> PipelineRunContextObjectPool => _lazyPipelineRunContextObjectPool!.Value;
    public static DefaultObjectPool<RetryPolicy> RetryPolicyObjectPool => _lazyRetryPolicyObjectPool!.Value;
    public static DefaultObjectPool<HandlerExecutionContext> ObjectPool => _lazyObjectPool!.Value;

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
