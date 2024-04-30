using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace M.EventBrokerSlim.Internal;

internal class HandlerExecutionContext
{
    private readonly DefaultObjectPool<RetryPolicy> _retryPolicyPool;
    private readonly SemaphoreSlim _semaphore;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<ThreadPoolEventHandlerRunner>? _logger;
    private readonly DefaultObjectPool<HandlerExecutionContext> _contextObjectPool;
    private readonly RetryQueue _retryQueue;

    public HandlerExecutionContext(DefaultObjectPool<RetryPolicy> retryPolicyPool, SemaphoreSlim semaphore, IServiceScopeFactory serviceScopeFactory, ILogger<ThreadPoolEventHandlerRunner>? logger, DefaultObjectPool<HandlerExecutionContext> contextObjectPool, RetryQueue retryQueue)
    {
        _retryPolicyPool = retryPolicyPool;
        _semaphore = semaphore;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _contextObjectPool = contextObjectPool;
        _retryQueue = retryQueue;
    }

    public HandlerExecutionContext Initialize(object @event, EventHandlerDescriptor eventHandlerDescriptor, RetryDescriptor? retryDescriptor, CancellationToken cancellationToken)
    {
        Event = @event;
        EventHandlerDescriptor = eventHandlerDescriptor;
        RetryDescriptor = retryDescriptor;
        CancellationToken = cancellationToken;

        RetryPolicy = RetryDescriptor is null ? _retryPolicyPool.Get() : RetryDescriptor.RetryPolicy;
        return this;
    }

    public void Clear()
    {
        Event = null;
        EventHandlerDescriptor = null;
        RetryDescriptor = null;
        CancellationToken = default;

        RetryPolicy = null;
    }

    public object? Event { get; private set; }

    public EventHandlerDescriptor? EventHandlerDescriptor { get; private set; }

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
                RetryDescriptor = new RetryDescriptor(Event!, EventHandlerDescriptor!, RetryPolicy);
            }

            await _retryQueue.Enqueue(RetryDescriptor).ConfigureAwait(false);
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

    public void LogEventHandlerResolvingError(Exception exception)
        => _logger?.LogEventHandlerResolvingError(Event!.GetType(), exception);

    public void LogUnhandledExceptionFromOnError(Type serviceType, Exception exception)
        => _logger?.LogUnhandledExceptionFromOnError(serviceType, exception);
}
