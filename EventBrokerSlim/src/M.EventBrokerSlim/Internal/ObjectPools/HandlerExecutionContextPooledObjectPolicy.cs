using System.Threading;
using FuncPipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace M.EventBrokerSlim.Internal.ObjectPools;

internal sealed class HandlerExecutionContextPooledObjectPolicy : IPooledObjectPolicy<HandlerExecutionContext>
{
    private readonly SemaphoreSlim _semaphore;
    private readonly RetryQueue _retryQueue;
    private readonly DefaultObjectPool<PipelineRunContext> _pipelineRunContextObjectPool;

    internal HandlerExecutionContextPooledObjectPolicy(
        SemaphoreSlim semaphore,
        RetryQueue retryQueue,
        int maxConcurrentHandlers)
    {
        _semaphore = semaphore;
        _retryQueue = retryQueue;
        _pipelineRunContextObjectPool = new DefaultObjectPool<PipelineRunContext>(new PipelineRunContextPooledObjectPolicy(), maxConcurrentHandlers);
    }

    public DefaultObjectPool<HandlerExecutionContext>? HandlerExecutionContextObjectPool { get; internal set; }

    public HandlerExecutionContext Create()
        => new HandlerExecutionContext(_semaphore, _retryQueue, _pipelineRunContextObjectPool, HandlerExecutionContextObjectPool);

    public bool Return(HandlerExecutionContext obj)
    {
        obj.Clear();
        return true;
    }
}
