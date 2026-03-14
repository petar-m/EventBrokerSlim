using System.Threading;
using FuncPipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace M.EventBrokerSlim.Internal.ObjectPools;

internal sealed class HandlerExecutionContextPooledObjectPolicy : IPooledObjectPolicy<HandlerExecutionContext>
{
    private readonly SemaphoreSlim _semaphore;
    private readonly ILogger _logger;
    private readonly RetryQueue _retryQueue;
    private readonly DefaultObjectPool<PipelineRunContext> _pipelineRunContextObjectPool;

    internal HandlerExecutionContextPooledObjectPolicy(
        SemaphoreSlim semaphore,
        ILogger logger,
        RetryQueue retryQueue,
        int maxConcurrentHandlers)
    {
        _semaphore = semaphore;
        _logger = logger;
        _retryQueue = retryQueue;
        _pipelineRunContextObjectPool = new DefaultObjectPool<PipelineRunContext>(new PipelineRunContextPooledObjectPolicy(), maxConcurrentHandlers);
    }

    public DefaultObjectPool<HandlerExecutionContext>? HandlerExecutionContextObjectPool { get; internal set; }

    public HandlerExecutionContext Create()
        => new HandlerExecutionContext(_semaphore, _logger, _retryQueue, _pipelineRunContextObjectPool, HandlerExecutionContextObjectPool);

    public bool Return(HandlerExecutionContext obj)
    {
        obj.Clear();
        return true;
    }
}
