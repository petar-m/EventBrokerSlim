using System.Threading;
using FuncPipeline;
using Microsoft.Extensions.Logging;

namespace M.EventBrokerSlim.Internal;

internal sealed class HandlerExecutionContext
{
    public HandlerExecutionContext(
        SemaphoreSlim semaphore,
        ILogger logger,
        RetryQueue retryQueue)
    {
        Semaphore = semaphore;
        Logger = logger;
        RetryQueue = retryQueue;
    }

    public HandlerExecutionContext Initialize(object @event, IPipeline pipeline, RetryDescriptor? retryDescriptor, CancellationToken cancellationToken)
    {
        Event = @event;
        Pipeline = pipeline;
        RetryDescriptor = retryDescriptor;
        CancellationToken = cancellationToken;
        RetryPolicy = RetryDescriptor?.RetryPolicy;
        return this;
    }

    public void Clear()
    {
        Event = null;
        Pipeline = null;
        RetryDescriptor = null;
        CancellationToken = default;
        RetryPolicy = null;
    }

    public object? Event { get; private set; }

    public IPipeline? Pipeline { get; private set; }

    public RetryDescriptor? RetryDescriptor { get; private set; }

    public CancellationToken CancellationToken { get; private set; }

    public RetryPolicy? RetryPolicy { get; private set; }
    public SemaphoreSlim Semaphore { get; }
    public ILogger Logger { get; }
    public RetryQueue RetryQueue { get; }
}
