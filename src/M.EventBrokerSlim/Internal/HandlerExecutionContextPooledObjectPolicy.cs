using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace M.EventBrokerSlim.Internal;

internal class HandlerExecutionContextPooledObjectPolicy : IPooledObjectPolicy<HandlerExecutionContext>
{
    private readonly DefaultObjectPool<RetryPolicy> _retryPolicyPool;
    private readonly SemaphoreSlim _semaphore;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<ThreadPoolEventHandlerRunner> _logger;
    private readonly RetryQueue _retryQueue;

    internal HandlerExecutionContextPooledObjectPolicy(
        DefaultObjectPool<RetryPolicy> retryPolicyPool,
        SemaphoreSlim semaphore,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ThreadPoolEventHandlerRunner> logger,
        RetryQueue retryQueue)
    {
        _retryPolicyPool = retryPolicyPool;
        _semaphore = semaphore;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _retryQueue = retryQueue;
    }

    internal DefaultObjectPool<HandlerExecutionContext>? ContextObjectPool { get; set; }

    public HandlerExecutionContext Create()
        => new HandlerExecutionContext(_retryPolicyPool, _semaphore, _serviceScopeFactory, _logger, ContextObjectPool!, _retryQueue);

    public bool Return(HandlerExecutionContext obj)
    {
        obj.Clear();
        return true;
    }
}
