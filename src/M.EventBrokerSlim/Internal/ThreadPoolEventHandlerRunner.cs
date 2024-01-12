using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace M.EventBrokerSlim.Internal;

internal sealed class ThreadPoolEventHandlerRunner
{
    private readonly ChannelReader<object> _channelReader;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly EventHandlerRegistry _eventHandlerRegistry;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly ILogger<ThreadPoolEventHandlerRunner>? _logger;
    private readonly SemaphoreSlim _semaphore;

    internal ThreadPoolEventHandlerRunner(
        ChannelReader<object> channelReader,
        IServiceScopeFactory serviceScopeFactory,
        EventHandlerRegistry eventHandlerRegistry,
        CancellationTokenSource cancellationTokenSource,
        ILogger<ThreadPoolEventHandlerRunner>? logger)
    {
        _channelReader = channelReader;
        _serviceScopeFactory = serviceScopeFactory;
        _eventHandlerRegistry = eventHandlerRegistry;
        _cancellationTokenSource = cancellationTokenSource;
        _logger = logger;
        _semaphore = new SemaphoreSlim(_eventHandlerRegistry.MaxConcurrentHandlers, _eventHandlerRegistry.MaxConcurrentHandlers);
    }

    public void Run()
    {
        _ = Task.Factory.StartNew(ProcessEvents, TaskCreationOptions.LongRunning | TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private async ValueTask ProcessEvents()
    {
        CancellationToken token = _cancellationTokenSource.Token;
        while (await _channelReader.WaitToReadAsync(token).ConfigureAwait(false))
        {
            while (_channelReader.TryRead(out var @event))
            {
                var type = @event.GetType();

                var eventHandlers = _eventHandlerRegistry.GetEventHandlers(type);
                if (eventHandlers is null)
                {
                    if (!_eventHandlerRegistry.DisableMissingHandlerWarningLog && _logger is not null)
                    {
                        _logger.LogNoEventHandlerForEvent(type);
                    }

                    continue;
                }

                for (int i = 0; i < eventHandlers.Count; i++)
                {
                    await _semaphore.WaitAsync(token).ConfigureAwait(false);

                    var eventHandlerDescriptor = eventHandlers[i];

                    _ = Task.Run(async () =>
                    {
                        using var scope = _serviceScopeFactory.CreateScope();
                        object? service = null;
                        try
                        {
                            service = scope.ServiceProvider.GetRequiredKeyedService(eventHandlerDescriptor.InterfaceType, eventHandlerDescriptor.Key);
                            await eventHandlerDescriptor.Handle(service, @event, token).ConfigureAwait(false);
                        }
                        catch (Exception exception)
                        {
                            if (service is null)
                            {
                                _logger?.LogEventHandlerResolvingError(@event.GetType(), exception);
                                return;
                            }

                            try
                            {
                                await eventHandlerDescriptor.OnError(service, @event, exception, token).ConfigureAwait(false);
                            }
                            catch (Exception errorHandlingException)
                            {
                                // suppress further exeptions
                                _logger?.LogUnhandledExceptionFromOnError(service.GetType(), errorHandlingException);
                            }
                        }
                        finally
                        {
                            _semaphore.Release();
                        }
                    });
                }
            }
        }
    }
}
