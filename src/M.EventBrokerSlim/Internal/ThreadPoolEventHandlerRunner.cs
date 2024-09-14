using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.ObjectPool;

namespace M.EventBrokerSlim.Internal;

internal sealed class ThreadPoolEventHandlerRunner
{
    private readonly ChannelReader<object> _channelReader;
    private readonly EventHandlerRegistry _eventHandlerRegistry;
    private readonly DelegateHandlerRegistry _delegateHandlerRegistry;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly ILogger<ThreadPoolEventHandlerRunner> _logger;
    private readonly DynamicEventHandlers _dynamicEventHandlers;
    private readonly SemaphoreSlim _semaphore;
    private readonly DefaultObjectPool<HandlerExecutionContext> _contextObjectPool;

    internal ThreadPoolEventHandlerRunner(
        Channel<object> channel,
        IServiceScopeFactory serviceScopeFactory,
        EventHandlerRegistry eventHandlerRegistry,
        DelegateHandlerRegistry delegateHandlerRegistry,
        CancellationTokenSource cancellationTokenSource,
        ILogger<ThreadPoolEventHandlerRunner>? logger,
        DynamicEventHandlers dynamicEventHandlers)
    {
        _channelReader = channel.Reader;
        _eventHandlerRegistry = eventHandlerRegistry;
        _delegateHandlerRegistry = delegateHandlerRegistry;
        _cancellationTokenSource = cancellationTokenSource;
        _logger = logger ?? new NullLogger<ThreadPoolEventHandlerRunner>();
        _dynamicEventHandlers = dynamicEventHandlers;
        _semaphore = new SemaphoreSlim(_eventHandlerRegistry.MaxConcurrentHandlers, _eventHandlerRegistry.MaxConcurrentHandlers);

        var retryQueue = new RetryQueue(channel.Writer, cancellationTokenSource.Token);
        var retryPolicyPool = new DefaultObjectPool<RetryPolicy>(new RetryPolicyPooledObjectPolicy(), _eventHandlerRegistry.MaxConcurrentHandlers);
        var executorPool = new DefaultObjectPool<Executor>(new ExecutorPooledObjectPolicy(), _eventHandlerRegistry.MaxConcurrentHandlers * _delegateHandlerRegistry.MaxPipelineLength());
        var delegateParametersArrayObjectPool = new DefaultObjectPool<object[]>(new DelegateParameterArrayPooledObjectPolicy(), _eventHandlerRegistry.MaxConcurrentHandlers);
        var contextPooledObjectPolicy = new HandlerExecutionContextPooledObjectPolicy(retryPolicyPool, _semaphore, serviceScopeFactory, _logger, retryQueue, delegateParametersArrayObjectPool, executorPool);
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
                    Type type = @event.GetType();
                    ImmutableArray<EventHandlerDescriptor> eventHandlers = _eventHandlerRegistry.GetEventHandlers(type);
                    ImmutableArray<DelegateHandlerDescriptor> delegateEventHandlers = _delegateHandlerRegistry.GetHandlers(type);
                    ImmutableList<DelegateHandlerDescriptor>? dynamicEventHandlers = _dynamicEventHandlers.GetDelegateHandlerDescriptors(type);

                    if(eventHandlers.Length == 0 &&
                       delegateEventHandlers.Length == 0 &&
                       (dynamicEventHandlers is null || dynamicEventHandlers.IsEmpty))
                    {
                        if(!_eventHandlerRegistry.DisableMissingHandlerWarningLog)
                        {
                            _logger.LogNoEventHandlerForEvent(type);
                        }

                        continue;
                    }

                    for(int i = 0; i < eventHandlers.Length; i++)
                    {
                        await _semaphore.WaitAsync(token).ConfigureAwait(false);

                        EventHandlerDescriptor eventHandlerDescriptor = eventHandlers[i];

                        HandlerExecutionContext context = _contextObjectPool.Get().Initialize(@event, eventHandlerDescriptor, null, retryDescriptor, token);
                        _ = Task.Factory.StartNew(static async x => await HandleEvent(x!), context);
                    }

                    for(int i = 0; i < delegateEventHandlers.Length; i++)
                    {
                        await _semaphore.WaitAsync(token).ConfigureAwait(false);

                        DelegateHandlerDescriptor delegateHandlerDescriptor = delegateEventHandlers[i];

                        HandlerExecutionContext context = _contextObjectPool.Get().Initialize(@event, null, delegateHandlerDescriptor, retryDescriptor, token);
                        _ = Task.Factory.StartNew(static async x => await HandleEventWithDelegate(x!), context);
                    }

                    if(dynamicEventHandlers is null || dynamicEventHandlers.IsEmpty)
                    {
                        continue;
                    }

                    for(int i = 0; i < dynamicEventHandlers.Count; i++)
                    {
                        await _semaphore.WaitAsync(token).ConfigureAwait(false);

                        DelegateHandlerDescriptor delegateHandlerDescriptor = dynamicEventHandlers[i];
                        if(delegateHandlerDescriptor.ClaimTicket is null)
                        {
                            continue;
                        }

                        HandlerExecutionContext context = _contextObjectPool.Get().Initialize(@event, null, delegateHandlerDescriptor, retryDescriptor, token);
                        _ = Task.Factory.StartNew(static async x => await HandleEventWithDelegate(x!), context);
                    }
                }
                else
                {
                    await _semaphore.WaitAsync(token).ConfigureAwait(false);

                    HandlerExecutionContext? context = null;
                    if(retryDescriptor is EventHandlerRetryDescriptor)
                    {
                        context = _contextObjectPool.Get().Initialize(retryDescriptor.Event, ((EventHandlerRetryDescriptor)retryDescriptor).EventHandlerDescriptor, null, retryDescriptor, token);
                        _ = Task.Factory.StartNew(static async x => await HandleEvent(x!), context);
                    }
                    else if(retryDescriptor is DelegateHandlerRetryDescriptor)
                    {
                        context = _contextObjectPool.Get().Initialize(retryDescriptor.Event, null, ((DelegateHandlerRetryDescriptor)retryDescriptor).DelegateHandlerDescriptor, retryDescriptor, token);
                        _ = Task.Factory.StartNew(static async x => await HandleEventWithDelegate(x!), context);
                    }
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
                // suppress further exceptions
                context.LogUnhandledExceptionFromOnError(service.GetType(), errorHandlingException);
            }
        }
        finally
        {
            await context.CompleteAsync().ConfigureAwait(false);
        }
    }

    private static async Task HandleEventWithDelegate(object state)
    {
        var context = (HandlerExecutionContext)state;
        var retryPolicy = context.RetryPolicy!;
        var @event = context.Event!;

        if(context.CancellationToken.IsCancellationRequested)
        {
            return;
        }

        DelegateHandlerDescriptor handlerDescriptor = context.DelegateHandlerDescriptor!;

        object[] services = context.BorrowDelegateParametersArray();
        using var scope = context.CreateScope();
        var executor = context.BorrowExecutor().Initialize(scope, handlerDescriptor, context.Event!, services, retryPolicy, context.CancellationToken);
        try
        {
            await executor.Execute();
        }
        catch(Exception exception)
        {
            context.LogDelegateEventHandlerError(handlerDescriptor.EventType, exception);
        }
        finally
        {
            context.ReturnDelegateParametersArray(services);
            context.ReturnExecutor(executor);
            await context.CompleteAsync().ConfigureAwait(false);
        }
    }

    internal class Executor : INextHandler
    {
        private static readonly int _endOfPipeline = -1;

#pragma warning disable CS8618 // Justification: Due to object pooling, these cannot be passed in the constructor.
        private IServiceScope _scope;
        private DelegateHandlerDescriptor _handler;
        private object _event;
        private object[] _parameters;
        private IRetryPolicy _retryPolicy;
        private CancellationToken _cancellationToken;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        private int _currentHandler;

        public Executor Initialize(IServiceScope scope, DelegateHandlerDescriptor handler, object @event, object[] parameters, IRetryPolicy retryPolicy, CancellationToken cancellationToken)
        {
            _scope = scope;
            _handler = handler;
            _event = @event;
            _parameters = parameters;
            _retryPolicy = retryPolicy;
            _cancellationToken = cancellationToken;

            _currentHandler = handler.Pipeline.Count > 0 ? handler.Pipeline.Count - 1 : _endOfPipeline;
            return this;
        }

        public async Task Execute()
        {
            if(HandlerAlreadyExecuted())
            {
                // if handler call INextHandler.Execute() - do nothing, causing it to continue execution
                return;
            }

            var handler = ShouldExecuteHandler() ? _handler : _handler.Pipeline[_currentHandler];
            _currentHandler--;
            Array.Clear(_parameters);
            for(int i = 0; i < handler.ParamTypes.Length; i++)
            {
                if(handler.ParamTypes[i] == _event.GetType())
                {
                    _parameters[i] = _event;
                }
                else if(handler.ParamTypes[i] == typeof(CancellationToken))
                {
                    _parameters[i] = _cancellationToken;
                }
                else if(handler.ParamTypes[i] == typeof(IRetryPolicy))
                {
                    _parameters[i] = _retryPolicy;
                }
                else if(handler.ParamTypes[i] == typeof(INextHandler))
                {
                    _parameters[i] = this;
                }
                else
                {
                    _parameters[i] = _scope.ServiceProvider.GetRequiredService(handler.ParamTypes[i]);
                }
            }

            await DelegateHelper.ExecuteDelegateHandler(handler.Handler, _parameters, handler.ParamTypes.Length).ConfigureAwait(false);
        }

        private bool ShouldExecuteHandler() => _currentHandler == -1;

        private bool HandlerAlreadyExecuted() => _currentHandler < -1;

        internal void Clear()
        {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
            _scope = null;
            _handler = null;
            _event = null;
            _parameters = null;
            _retryPolicy = null;
            _cancellationToken = default;
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
            _currentHandler = 0;
        }
    }
}
