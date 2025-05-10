using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace M.EventBrokerSlim.Internal.ObjectPools;

internal sealed class HandlerExecutionContextPooledObjectPolicy : IPooledObjectPolicy<HandlerExecutionContext>
{
    private readonly SemaphoreSlim _semaphore;
    private readonly ILogger _logger;
    private readonly RetryQueue _retryQueue;

    internal HandlerExecutionContextPooledObjectPolicy(
        SemaphoreSlim semaphore,
        ILogger logger,
        RetryQueue retryQueue)
    {
        _semaphore = semaphore;
        _logger = logger;
        _retryQueue = retryQueue;
    }

    public HandlerExecutionContext Create()
        => new HandlerExecutionContext(_semaphore, _logger, _retryQueue);

    public bool Return(HandlerExecutionContext obj)
    {
        obj.Clear();
        return true;
    }
}
