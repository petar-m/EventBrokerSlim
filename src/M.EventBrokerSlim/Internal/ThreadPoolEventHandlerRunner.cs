using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace M.EventBrokerSlim.Internal;

internal sealed class ThreadPoolEventHandlerRunner
{
    private readonly ChannelReader<object> _channelReader;
    private readonly EventHandlerRegistry _eventHandlerRegistry;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly ILogger<ThreadPoolEventHandlerRunner>? _logger;
    private readonly SemaphoreSlim _semaphore;
    private readonly DefaultObjectPool<HandlerExecutionContext> _contextObjectPool;

    internal ThreadPoolEventHandlerRunner(
        Channel<object> channel,
        IServiceScopeFactory serviceScopeFactory,
        EventHandlerRegistry eventHandlerRegistry,
        CancellationTokenSource cancellationTokenSource,
        ILogger<ThreadPoolEventHandlerRunner>? logger)
    {
        _channelReader = channel.Reader;
        _eventHandlerRegistry = eventHandlerRegistry;
        _cancellationTokenSource = cancellationTokenSource;
        _logger = logger;
        _semaphore = new SemaphoreSlim(_eventHandlerRegistry.MaxConcurrentHandlers, _eventHandlerRegistry.MaxConcurrentHandlers);

        var retryQueue = new RetryQueue(channel.Writer, cancellationTokenSource.Token);
        var retryPolicyPool = new DefaultObjectPool<RetryPolicy>(new RetryPolicyPooledObjectPolicy(), _eventHandlerRegistry.MaxConcurrentHandlers);
        var contextPooledObjectPolicy = new HandlerExecutionContextPooledObjectPolicy(retryPolicyPool, _semaphore, serviceScopeFactory, _logger, retryQueue);
        _contextObjectPool = new DefaultObjectPool<HandlerExecutionContext>(contextPooledObjectPolicy, _eventHandlerRegistry.MaxConcurrentHandlers);
        contextPooledObjectPolicy.ContextObjectPool = _contextObjectPool;
    }

    public void Run()
    {
        _ = Task.Factory.StartNew(ProcessEvents, TaskCreationOptions.LongRunning | TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private async ValueTask ProcessEvents()
    {
        CancellationToken token = _cancellationTokenSource.Token;
        while(await _channelReader.WaitToReadAsync(token).ConfigureAwait(false))
        {
            while(_channelReader.TryRead(out var @event))
            {
                RetryDescriptor? retryDescriptor = @event as RetryDescriptor;
                if(retryDescriptor is null)
                {
                    var type = @event.GetType();
                    var eventHandlers = _eventHandlerRegistry.GetEventHandlers(type);
                    if(eventHandlers == default)
                    {
                        if(!_eventHandlerRegistry.DisableMissingHandlerWarningLog && _logger is not null)
                        {
                            _logger.LogNoEventHandlerForEvent(type);
                        }

                        continue;
                    }

                    for(int i = 0; i < eventHandlers.Length; i++)
                    {
                        await _semaphore.WaitAsync(token).ConfigureAwait(false);

                        var eventHandlerDescriptor = eventHandlers[i];

                        var context = _contextObjectPool.Get().Initialize(@event, eventHandlerDescriptor, retryDescriptor, token);
                        _ = Task.Factory.StartNew(static async x => await HandleEvent(x!), context);
                    }
                }
                else
                {
                    await _semaphore.WaitAsync(token).ConfigureAwait(false);

                    var context = _contextObjectPool.Get().Initialize(retryDescriptor.Event, retryDescriptor.EventHandlerDescriptor, retryDescriptor, token);
                    _ = Task.Factory.StartNew(static async x => await HandleEvent(x!), context);
                }
            }
        }
    }

    private static async Task HandleEvent(object state)
    {
        var context = (HandlerExecutionContext)state;
        var retryPolicy = context.RetryPolicy!;
        var @event = context.Event!;
        var handler = context.EventHandlerDescriptor!.Handle;
        var errorHandler = context.EventHandlerDescriptor!.OnError;

        if(context.CancellationToken.IsCancellationRequested)
        {
            return;
        }

        object? service = null;
        using var scope = context.CreateScope();
        try
        {
            service = context.GetService(scope.ServiceProvider);

            await handler(service, @event, retryPolicy, context.CancellationToken).ConfigureAwait(false);
        }
        catch(Exception exception)
        {
            if(service is null)
            {
                context.LogEventHandlerResolvingError(exception);
                return;
            }

            try
            {
                await errorHandler(service, @event, exception, retryPolicy, context.CancellationToken).ConfigureAwait(false);
            }
            catch(Exception errorHandlingException)
            {
                // suppress further exeptions
                context.LogUnhandledExceptionFromOnError(service.GetType(), errorHandlingException);
            }
        }
        finally
        {
            await context.CompleteAsync().ConfigureAwait(false);
        }
    }
}
