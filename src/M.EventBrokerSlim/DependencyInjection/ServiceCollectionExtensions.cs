using System;
using System.Threading.Channels;
using M.EventBrokerSlim.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace M.EventBrokerSlim.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEventBroker(this IServiceCollection serviceCollection, Action<EventHandlerRegistryBuilder> handlersConfiguration)
    {
        var eventHandlerRegistryBuilder = new EventHandlerRegistryBuilder(serviceCollection);
        handlersConfiguration(eventHandlerRegistryBuilder);

        serviceCollection.AddSingleton(eventHandlerRegistryBuilder.Build());

        var channelKey = Guid.NewGuid();

        serviceCollection.AddKeyedSingleton(
            channelKey,
            (_, _) => Channel.CreateUnbounded<object>(new UnboundedChannelOptions
            {
                AllowSynchronousContinuations = false,
                SingleReader = true,
                SingleWriter = false
            }));

        serviceCollection.AddSingleton<IEventBroker>(
            x =>
            {
                var eventHandlerRunner = x.GetRequiredService<ThreadPoolEventHandlerRunner>();
                eventHandlerRunner.Run();
                return new EventBroker(x.GetRequiredKeyedService<Channel<object>>(channelKey).Writer);
            });

        serviceCollection.AddSingleton(
            x => new ThreadPoolEventHandlerRunner(
                x.GetRequiredKeyedService<Channel<object>>(channelKey).Reader,
                x.GetRequiredService<IServiceScopeFactory>(),
                x.GetRequiredService<EventHandlerRegistry>(),
                x.GetService<ILogger<ThreadPoolEventHandlerRunner>>()));

        return serviceCollection;
    }
}
