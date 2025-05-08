using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using FuncPipeline;
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
        var eventBrokerBuilder = new EventBrokerBuilder(serviceCollection);
        eventBrokerConfiguration?.Invoke(eventBrokerBuilder);

        var eventBrokerKey = Guid.NewGuid();

        CancellationTokenSource eventBrokerCancellationTokenSource = new();
        serviceCollection.AddKeyedSingleton(eventBrokerKey, eventBrokerCancellationTokenSource);

        serviceCollection.AddKeyedSingleton(
            eventBrokerKey,
            (_, _) => Channel.CreateUnbounded<object>(new UnboundedChannelOptions
            {
                AllowSynchronousContinuations = false,
                SingleReader = true,
                SingleWriter = false
            }));

        serviceCollection.AddSingleton<IEventBroker>(
            x =>
            {
                var eventHandlerRunner = x.GetRequiredKeyedService<ThreadPoolEventHandlerRunner>(eventBrokerKey);
                eventHandlerRunner.Run();
                return new EventBroker(
                    x.GetRequiredKeyedService<Channel<object>>(eventBrokerKey).Writer,
                    x.GetRequiredKeyedService<CancellationTokenSource>(eventBrokerKey));
            });

        serviceCollection.AddSingleton<DynamicEventHandlers>();
        serviceCollection.AddSingleton<IDynamicEventHandlers>(x => x.GetRequiredService<DynamicEventHandlers>());

        serviceCollection.AddKeyedSingleton(
            eventBrokerKey,
            (x, key) => new ThreadPoolEventHandlerRunner(
                x.GetRequiredKeyedService<Channel<object>>(eventBrokerKey),
                x.GetRequiredService<IServiceScopeFactory>(),
                x.GetRequiredService<PipelineRegistry>(),
                x.GetRequiredKeyedService<CancellationTokenSource>(eventBrokerKey),
                x.GetService<ILogger<ThreadPoolEventHandlerRunner>>(),
                x.GetRequiredService<DynamicEventHandlers>(),
                new EventBrokerSettings(eventBrokerBuilder._maxConcurrentHandlers, eventBrokerBuilder._disableMissingHandlerWarningLog)));

        serviceCollection.AddSingleton(
            x =>
            {
                var pipelines = x.GetServices<EventPipeline>();
                return new PipelineRegistry(pipelines, x.GetRequiredService<IServiceScopeFactory>());
            });

        return serviceCollection;
    }

    /// <summary>
    /// Adds a scoped service implementing <see cref="IEventHandler{TEvent}"/> to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <typeparam name="TEvent">The type of the event handled.</typeparam>
    /// <typeparam name="THandler">The type of the <see cref="IEventHandler{TEvent}"/> implementation.</typeparam>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public static IServiceCollection AddScopedEventHandler<TEvent, THandler>(this IServiceCollection services, string? key = null) where THandler : class, IEventHandler<TEvent>
    {
        services.AddScoped<IEventHandler<TEvent>, THandler>();
        key ??= Guid.NewGuid().ToString();
        services.AddKeyedScoped<IEventHandler<TEvent>, THandler>(key);
        services.AddSingleton(CreateEventPipeline<TEvent>(key));
        return services;
    }

    public static IServiceCollection AddSingletonEventHandler<TEvent, THandler>(this IServiceCollection services, string? key = null) where THandler : class, IEventHandler<TEvent>
    {
        services.AddSingleton<IEventHandler<TEvent>, THandler>();
        key ??= Guid.NewGuid().ToString();
        services.AddKeyedSingleton<IEventHandler<TEvent>, THandler>(key);
        services.AddSingleton(CreateEventPipeline<TEvent>(key));
        return services;
    }

    public static IServiceCollection AddTransientEventHandler<TEvent, THandler>(this IServiceCollection services, string? key = null) where THandler : class, IEventHandler<TEvent>
    {
        services.AddTransient<IEventHandler<TEvent>, THandler>();
        key ??= Guid.NewGuid().ToString();
        services.AddKeyedTransient<IEventHandler<TEvent>, THandler>(key);
        services.AddSingleton(CreateEventPipeline<TEvent>(key));
        return services;
    }

    public static IServiceCollection AddEventHandlerPileline<TEvent>(this IServiceCollection services, IPipeline pipeline)
        => services.AddSingleton(new EventPipeline(typeof(TEvent), pipeline));

    private static EventPipeline CreateEventPipeline<TEvent>(string key)
    {
        var pipeline = PipelineBuilder.Create()
            .NewPipeline()
            .Execute(static async (ILogger logger, INext next) =>
            {
                try
                {
                    await next.RunAsync().ConfigureAwait(false);
                }
                catch(Exception x)
                {
                    logger?.LogUnhandledExceptionFromOnError(typeof(IEventHandler<TEvent>), x);
                }
            })
            .Execute(static async (
                IEventHandler<TEvent> handler,
                [ResolveFrom(PrimarySource = Source.Context, Fallback = false, PrimaryNotFound = NotFoundBehavior.ThrowException)]
                TEvent @event,
                [ResolveFrom(PrimarySource = Source.Context, Fallback = false, PrimaryNotFound = NotFoundBehavior.ThrowException)]
                IRetryPolicy retryPolicy,
                [ResolveFrom(PrimarySource = Source.Context, Fallback = false, PrimaryNotFound = NotFoundBehavior.ThrowException)]
                CancellationToken ct,
                ILogger logger) =>
            {
                try
                {
                    await handler.Handle(@event, retryPolicy, ct).ConfigureAwait(false);
                }
                catch(Exception x)
                {
                    await handler.OnError(x, @event, retryPolicy, ct).ConfigureAwait(false);
                }
            },
            new Dictionary<int, ResolveFromAttribute>
            {
                {
                    0,
                    new ResolveFromAttribute
                    {
                        PrimarySource = Source.Services,
                        Fallback = false,
                        PrimaryNotFound = NotFoundBehavior.ThrowException,
                        Key = key
                    }
                },
                {
                    1,
                    new ResolveFromAttribute
                    {
                        PrimarySource = Source.Context,
                        Fallback = false,
                        PrimaryNotFound = NotFoundBehavior.ThrowException,
                        Key = key
                    }
                }
            })
            .Build();

        return new EventPipeline(typeof(TEvent), pipeline.Pipelines[0]);
    }
}
