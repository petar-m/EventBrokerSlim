using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace M.EventBrokerSlim.Internal;

internal sealed class HandlerExecutionContext
{
    private readonly DefaultObjectPool<RetryPolicy> _retryPolicyPool;
    private readonly SemaphoreSlim _semaphore;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<ThreadPoolEventHandlerRunner> _logger;
    private readonly DefaultObjectPool<HandlerExecutionContext> _contextObjectPool;
    private readonly RetryQueue _retryQueue;
    private readonly DefaultObjectPool<object[]> _delegateParametersArrayObjectPool;
    private readonly DefaultObjectPool<ThreadPoolEventHandlerRunner.Executor> _executorPool;

    public HandlerExecutionContext(
        DefaultObjectPool<RetryPolicy> retryPolicyPool,
        SemaphoreSlim semaphore,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ThreadPoolEventHandlerRunner> logger,
        DefaultObjectPool<HandlerExecutionContext> contextObjectPool,
        RetryQueue retryQueue,
        DefaultObjectPool<object[]> delegateParametersArrayObjectPool,
        DefaultObjectPool<ThreadPoolEventHandlerRunner.Executor> executorPool)
    {
        _retryPolicyPool = retryPolicyPool;
        _semaphore = semaphore;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _contextObjectPool = contextObjectPool;
        _retryQueue = retryQueue;
        _delegateParametersArrayObjectPool = delegateParametersArrayObjectPool;
        _executorPool = executorPool;
    }

    public HandlerExecutionContext Initialize(object @event, EventHandlerDescriptor? eventHandlerDescriptor, DelegateHandlerDescriptor? delegateHandlerDescriptor, RetryDescriptor? retryDescriptor, CancellationToken cancellationToken)
    {
        Event = @event;
        EventHandlerDescriptor = eventHandlerDescriptor;
        DelegateHandlerDescriptor = delegateHandlerDescriptor;
        RetryDescriptor = retryDescriptor;
        CancellationToken = cancellationToken;

        RetryPolicy = RetryDescriptor is null ? _retryPolicyPool.Get() : RetryDescriptor.RetryPolicy;
        return this;
    }

    public void Clear()
    {
        Event = null;
        EventHandlerDescriptor = null;
        DelegateHandlerDescriptor = null;
        RetryDescriptor = null;
        CancellationToken = default;

        RetryPolicy = null;
    }

    public object? Event { get; private set; }

    public EventHandlerDescriptor? EventHandlerDescriptor { get; private set; }

    public DelegateHandlerDescriptor? DelegateHandlerDescriptor { get; private set; }

    public RetryDescriptor? RetryDescriptor { get; private set; }

    public CancellationToken CancellationToken { get; private set; }

    public RetryPolicy? RetryPolicy { get; private set; }

    public async Task CompleteAsync()
    {
        if(RetryPolicy!.RetryRequested)
        {
            RetryPolicy.NextAttempt();
            // first retry
            if(RetryDescriptor is null)
            {
                if(EventHandlerDescriptor is not null)
                {
                    RetryDescriptor = new EventHandlerRetryDescriptor(Event!, EventHandlerDescriptor!, RetryPolicy);
                }

                if(DelegateHandlerDescriptor is not null)
                {
                    RetryDescriptor = new DelegateHandlerRetryDescriptor(Event!, DelegateHandlerDescriptor!, RetryPolicy);
                }
            }

            await _retryQueue.Enqueue(RetryDescriptor!).ConfigureAwait(false);
        }
        else
        {
            _retryPolicyPool.Return(RetryPolicy);
        }

        _contextObjectPool.Return(this);
        _semaphore.Release();
    }

    public object GetService(IServiceProvider serviceProvider)
        => serviceProvider.GetRequiredKeyedService(EventHandlerDescriptor!.InterfaceType, EventHandlerDescriptor.Key);

    public IServiceScope CreateScope()
        => _serviceScopeFactory.CreateScope();

    public object[] BorrowDelegateParametersArray() => _delegateParametersArrayObjectPool.Get();

    public void ReturnDelegateParametersArray(object[] parametersArray) => _delegateParametersArrayObjectPool.Return(parametersArray);

    public ThreadPoolEventHandlerRunner.Executor BorrowExecutor() => _executorPool.Get();

    public void ReturnExecutor(ThreadPoolEventHandlerRunner.Executor executor) => _executorPool.Return(executor);

    internal void LogEventHandlerResolvingError(Exception exception)
        => _logger.LogEventHandlerResolvingError(Event!.GetType(), exception);

    internal void LogUnhandledExceptionFromOnError(Type serviceType, Exception exception)
        => _logger.LogUnhandledExceptionFromOnError(serviceType, exception);

    internal void LogDelegateEventHandlerError(Type eventType, Exception exception)
        => _logger.LogDelegateEventHandlerError(eventType, exception);
}
