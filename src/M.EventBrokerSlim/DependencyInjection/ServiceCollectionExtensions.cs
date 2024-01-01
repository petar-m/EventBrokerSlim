﻿using System;
using System.Threading.Channels;
using M.EventBrokerSlim.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace M.EventBrokerSlim.DependencyInjection;

/// <summary>
/// Extension methods for setting up event broker in an <see cref="IServiceCollection" />.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds event broker to the specified <see cref="IServiceCollection" />.
    /// </summary>
    /// <param name="serviceCollection">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <param name="eventBrokerConfiguration">The <see cref="EventBrokerBuilder"/> configuration delegate.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddEventBroker(
        this IServiceCollection serviceCollection,
        Action<EventBrokerBuilder>? eventBrokerConfiguration = null)
    {
        var eventHandlerRegistryBuilder = new EventBrokerBuilder(serviceCollection);
        eventBrokerConfiguration?.Invoke(eventHandlerRegistryBuilder);

        serviceCollection.AddSingleton<EventHandlerRegistryBuilder>(eventHandlerRegistryBuilder);

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

        serviceCollection.AddSingleton(
            x =>
            {
                var builders = x.GetServices<EventHandlerRegistryBuilder>();
                return EventHandlerRegistryBuilder.Build(builders);
            });

        return serviceCollection;
    }

    /// <summary>
    /// Adds EventBroker event handlers to the specified <see cref="IServiceCollection" />.
    /// </summary>
    /// <param name="serviceCollection">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <param name="eventHandlersConfiguration">The <see cref="EventHandlerRegistryBuilder"/> configuration delegate.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddEventHandlers(
        this IServiceCollection serviceCollection,
        Action<EventHandlerRegistryBuilder> eventHandlersConfiguration)
    {
        var eventHandlerRegistryBuilder = new EventHandlerRegistryBuilder(serviceCollection);
        eventHandlersConfiguration(eventHandlerRegistryBuilder);

        serviceCollection.AddSingleton(eventHandlerRegistryBuilder);
        return serviceCollection;
    }
}
