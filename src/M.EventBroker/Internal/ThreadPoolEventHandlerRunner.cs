using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace M.EventBrokerSlim.Internal;

/// <summary>
/// Runs event handlers on a ThreadPool threads.
/// </summary>
internal class ThreadPoolEventHandlerRunner
{
    private readonly ChannelReader<object> _channelReader;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly EventHandlerRegistry _eventHandlerRegistry;
    private readonly SemaphoreSlim _semaphore;

    internal ThreadPoolEventHandlerRunner(ChannelReader<object> channelReader, IServiceScopeFactory serviceScopeFactory, EventHandlerRegistry eventHandlerRegistry)
    {
        _channelReader = channelReader;
        _serviceScopeFactory = serviceScopeFactory;
        _eventHandlerRegistry = eventHandlerRegistry;
        _semaphore = new SemaphoreSlim(_eventHandlerRegistry.MaxConcurrentHandlers, _eventHandlerRegistry.MaxConcurrentHandlers);
    }

    public ValueTask Run()
    {
        _ = Task.Run(RunAsync);
        return ValueTask.CompletedTask;
    }

    private async ValueTask RunAsync()
    {
        while (await _channelReader.WaitToReadAsync())
        {
            while (_channelReader.TryRead(out var @event))
            {
                var type = @event.GetType();

                var eventHandlers = _eventHandlerRegistry.GetEventHandlers(type);
                if (eventHandlers is null)
                {
                    // TODO: Log?
                    continue;
                }

                for (int i = 0; i < eventHandlers.Count; i++)
                {
                    await _semaphore.WaitAsync();

                    var eventHandlerDescriptior = eventHandlers[i];

                    _ = Task.Run(async () =>
                    {
                        using var scope = _serviceScopeFactory.CreateScope();
                        object? service = null;
                        try
                        {
                            service = scope.ServiceProvider.GetRequiredKeyedService(eventHandlerDescriptior.InterfaceType, eventHandlerDescriptior.Key);
                            if (!await eventHandlerDescriptior.ShouldHandle(service, @event))
                            {
                                return;
                            }

                            await eventHandlerDescriptior.Handle(service, @event);
                        }
                        catch (Exception exception)
                        {
                            if (service is null)
                            {
                                // TODO: Log?
                                return;
                            }

                            try
                            {
                                await eventHandlerDescriptior.OnError(service, @event, exception);
                            }
                            catch
                            {
                                // supress further exeptions
                                // TODO: Log?
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
