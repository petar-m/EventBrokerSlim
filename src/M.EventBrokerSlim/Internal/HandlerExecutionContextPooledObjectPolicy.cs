using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace M.EventBrokerSlim.Internal;

internal sealed class HandlerExecutionContextPooledObjectPolicy : IPooledObjectPolicy<HandlerExecutionContext>
{
    private readonly DefaultObjectPool<RetryPolicy> _retryPolicyPool;
    private readonly SemaphoreSlim _semaphore;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<ThreadPoolEventHandlerRunner> _logger;
    private readonly RetryQueue _retryQueue;
    private readonly DefaultObjectPool<object[]> _delegateParametersArrayObjectPool;
    private readonly DefaultObjectPool<ThreadPoolEventHandlerRunner.Executor> _executorPool;

    internal HandlerExecutionContextPooledObjectPolicy(
        DefaultObjectPool<RetryPolicy> retryPolicyPool,
        SemaphoreSlim semaphore,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ThreadPoolEventHandlerRunner> logger,
        RetryQueue retryQueue,
        DefaultObjectPool<object[]> delegateParametersArrayObjectPool,
        DefaultObjectPool<ThreadPoolEventHandlerRunner.Executor> executorPool)
    {
        _retryPolicyPool = retryPolicyPool;
        _semaphore = semaphore;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _retryQueue = retryQueue;
        _delegateParametersArrayObjectPool = delegateParametersArrayObjectPool;
        _executorPool = executorPool;
    }

    internal DefaultObjectPool<HandlerExecutionContext>? ContextObjectPool { get; set; }

    public HandlerExecutionContext Create()
        => new HandlerExecutionContext(_retryPolicyPool, _semaphore, _serviceScopeFactory, _logger, ContextObjectPool!, _retryQueue, _delegateParametersArrayObjectPool, _executorPool);

    public bool Return(HandlerExecutionContext obj)
    {
        obj.Clear();
        return true;
    }
}
