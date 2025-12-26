using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Channels;
using FuncPipeline;
using M.EventBrokerSlim.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace M.EventBrokerSlim.DependencyInjection;

/// <summary>
/// Represents a pipeline for handling events.
/// </summary>
/// <param name="Event">The type of the event.</param>
/// <param name="Pipeline">The pipeline to process the event.</param>
public record EventPipeline(Type Event, IPipeline Pipeline);

/// <summary>
/// Extension methods for setting up event broker in an <see cref="IServiceCollection" />.
/// </summary>
public static class ServiceCollectionExtensions
{
    private static readonly object _defaultEventBrokerKey = Guid.NewGuid();

    /// <summary>
    /// Adds an event broker to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="serviceCollection">
    /// The <see cref="IServiceCollection"/> to add the event broker services to.
    /// </param>
    /// <param name="eventBrokerConfiguration">
    /// An optional delegate to configure the <see cref="EventBrokerBuilder"/> for customizing event broker behavior.
    /// </param>
    /// <returns>
    /// The <see cref="IServiceCollection"/> so that additional calls can be chained.
    /// </returns>
    public static IServiceCollection AddEventBroker(
        this IServiceCollection serviceCollection,
        Action<EventBrokerBuilder>? eventBrokerConfiguration = null)
    {
        return serviceCollection.AddKeyedEventBroker(_defaultEventBrokerKey, eventBrokerConfiguration);
    }

    /// <summary>
    /// Adds keyed event broker to the specified <see cref="IServiceCollection" />.
    /// </summary>
    /// <param name="serviceCollection">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <param name="eventBrokerKey">
    /// The key used to uniquely identify the event broker instance within the service collection.
    /// </param>
    /// <param name="eventBrokerConfiguration">
    /// An optional delegate to configure the <see cref="EventBrokerBuilder"/> for customizing event broker behavior.
    /// </param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddKeyedEventBroker(
        this IServiceCollection serviceCollection,
        object eventBrokerKey,
        Action<EventBrokerBuilder>? eventBrokerConfiguration = null)
    {
        var eventBrokerBuilder = new EventBrokerBuilder(serviceCollection);
        eventBrokerConfiguration?.Invoke(eventBrokerBuilder);

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

        serviceCollection.AddKeyedSingleton<DynamicEventHandlers>(eventBrokerKey);

        serviceCollection.AddKeyedSingleton(
            eventBrokerKey,
            (x, key) =>
            {
                var pipelines = x.GetKeyedServices<EventPipeline>(key);
                return new PipelineRegistry(pipelines, x.GetRequiredService<IServiceScopeFactory>());
            });

        if(eventBrokerKey == _defaultEventBrokerKey)
        {
            serviceCollection.AddSingleton<IEventBroker>(
                x =>
                {
                    var eventHandlerRunner = x.GetRequiredKeyedService<ThreadPoolEventHandlerRunner>(eventBrokerKey);
                    eventHandlerRunner.Run();
                    return new EventBroker(
                        x.GetRequiredKeyedService<Channel<object>>(eventBrokerKey).Writer,
                        x.GetRequiredKeyedService<CancellationTokenSource>(eventBrokerKey));
                });

            serviceCollection.AddSingleton<IDynamicEventHandlers>(x => x.GetRequiredKeyedService<DynamicEventHandlers>(eventBrokerKey));
            serviceCollection.AddSingleton(x => x.GetRequiredKeyedService<PipelineRegistry>(_defaultEventBrokerKey));
        }
        else
        {
            serviceCollection.AddKeyedSingleton<IEventBroker>(
                eventBrokerKey,
                (x, key) =>
                {
                    var eventHandlerRunner = x.GetRequiredKeyedService<ThreadPoolEventHandlerRunner>(key);
                    eventHandlerRunner.Run();
                    return new EventBroker(
                        x.GetRequiredKeyedService<Channel<object>>(key).Writer,
                        x.GetRequiredKeyedService<CancellationTokenSource>(key));
                });

            serviceCollection.AddKeyedSingleton<IDynamicEventHandlers>(eventBrokerKey, (x, key) => x.GetRequiredKeyedService<DynamicEventHandlers>(key));
        }

        serviceCollection.AddKeyedSingleton(
            eventBrokerKey,
            (x, key) => new ThreadPoolEventHandlerRunner(
                x.GetRequiredKeyedService<Channel<object>>(eventBrokerKey),
                x.GetRequiredService<IServiceScopeFactory>(),
                x.GetRequiredKeyedService<PipelineRegistry>(eventBrokerKey),
                x.GetRequiredKeyedService<CancellationTokenSource>(eventBrokerKey),
                x.GetService<ILogger<ThreadPoolEventHandlerRunner>>(),
                x.GetRequiredKeyedService<DynamicEventHandlers>(eventBrokerKey),
                new EventBrokerSettings(eventBrokerBuilder._maxConcurrentHandlers, eventBrokerBuilder._disableMissingHandlerWarningLog)));

        return serviceCollection;
    }

    /// <summary>
    /// Adds a scoped event handler service implementing <see cref="IEventHandler{TEvent}"/> to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <typeparam name="TEvent">
    /// The type of event the handler processes.
    /// </typeparam>
    /// <typeparam name="THandler">
    /// The concrete implementation of <see cref="IEventHandler{TEvent}"/> to register.
    /// </typeparam>
    /// <param name="services">
    /// The <see cref="IServiceCollection"/> to add the event handler to.
    /// </param>
    /// <param name="eventHandlerKey">
    /// An optional key to uniquely identify the event handler registration.
    /// </param>
    /// <param name="eventBrokerKey">
    /// An optional key to associate the handler with a specific event broker instance. If not provided, the default event broker is used.
    /// </param>
    /// <returns>
    /// The <see cref="IServiceCollection"/> so that additional calls can be chained.
    /// </returns>
    public static IServiceCollection AddScopedEventHandler<TEvent, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(this IServiceCollection services, string? eventHandlerKey = null, object? eventBrokerKey = null) where THandler : class, IEventHandler<TEvent>
    {
        eventHandlerKey ??= Guid.NewGuid().ToString();
        services.AddKeyedScoped<IEventHandler<TEvent>, THandler>(eventHandlerKey);
        services.AddKeyedSingleton(eventBrokerKey ?? _defaultEventBrokerKey, CreateEventPipeline<TEvent, THandler>(eventHandlerKey));
        return services;
    }

    /// <summary>
    /// Adds a singleton event handler service implementing <see cref="IEventHandler{TEvent}"/> to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <typeparam name="TEvent">
    /// The type of event the handler processes.
    /// </typeparam>
    /// <typeparam name="THandler">
    /// The concrete implementation of <see cref="IEventHandler{TEvent}"/> to register as a singleton.
    /// </typeparam>
    /// <param name="services">
    /// The <see cref="IServiceCollection"/> to add the event handler to.
    /// </param>
    /// <param name="eventHandlerKey">
    /// An optional key to uniquely identify the event handler registration.
    /// </param>
    /// <param name="eventBrokerKey">
    /// An optional key to associate the handler with a specific event broker instance. If not provided, the default event broker is used.
    /// </param>
    /// <returns>
    /// The <see cref="IServiceCollection"/> so that additional calls can be chained.
    /// </returns>
    public static IServiceCollection AddSingletonEventHandler<TEvent, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(this IServiceCollection services, string? eventHandlerKey = null, object? eventBrokerKey = null) where THandler : class, IEventHandler<TEvent>
    {
        eventHandlerKey ??= Guid.NewGuid().ToString();
        services.AddKeyedSingleton<IEventHandler<TEvent>, THandler>(eventHandlerKey);
        services.AddKeyedSingleton(eventBrokerKey ?? _defaultEventBrokerKey, CreateEventPipeline<TEvent, THandler>(eventHandlerKey));
        return services;
    }

    /// <summary>
    /// Adds a transient event handler service implementing <see cref="IEventHandler{TEvent}"/> to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <typeparam name="TEvent">
    /// The type of event the handler processes.
    /// </typeparam>
    /// <typeparam name="THandler">
    /// The concrete implementation of <see cref="IEventHandler{TEvent}"/> to register as a transient service.
    /// </typeparam>
    /// <param name="services">
    /// The <see cref="IServiceCollection"/> to add the event handler to.
    /// </param>
    /// <param name="eventHandlerKey">
    /// An optional key to uniquely identify the event handler registration.
    /// </param>
    /// <param name="eventBrokerKey">
    /// An optional key to associate the handler with a specific event broker instance. If not provided, the default event broker is used.
    /// </param>
    /// <returns>
    /// The <see cref="IServiceCollection"/> so that additional calls can be chained.
    /// </returns>
    public static IServiceCollection AddTransientEventHandler<TEvent, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(this IServiceCollection services, string? eventHandlerKey = null, object? eventBrokerKey = null) where THandler : class, IEventHandler<TEvent>
    {
        eventHandlerKey ??= Guid.NewGuid().ToString();
        services.AddKeyedTransient<IEventHandler<TEvent>, THandler>(eventHandlerKey);
        services.AddKeyedSingleton(eventBrokerKey ?? _defaultEventBrokerKey, CreateEventPipeline<TEvent, THandler>(eventHandlerKey));
        return services;
    }

    /// <summary>
    /// Adds a pipeline for handling a specific event type to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <typeparam name="TEvent">
    /// The type of event the pipeline will handle.
    /// </typeparam>
    /// <param name="services">
    /// The <see cref="IServiceCollection"/> to add the pipeline to.
    /// </param>
    /// <param name="pipeline">
    /// The <see cref="IPipeline"/> instance that defines the processing logic for the event type.
    /// </param>
    /// <param name="eventBrokerKey">
    /// An optional key to associate the pipeline with a specific event broker instance. If not provided, the default event broker is used.
    /// </param>
    /// <returns>
    /// The <see cref="IServiceCollection"/> so that additional calls can be chained.
    /// </returns>
    public static IServiceCollection AddEventHandlerPipeline<TEvent>(this IServiceCollection services, IPipeline pipeline, object? eventBrokerKey = null)
        => services.AddKeyedSingleton(eventBrokerKey ?? _defaultEventBrokerKey, new EventPipeline(typeof(TEvent), pipeline));

    private static EventPipeline CreateEventPipeline<TEvent, THandler>(string key) where THandler : class, IEventHandler<TEvent>
    {
        var pipeline = PipelineBuilder.Create()
            .NewPipeline()
            .Execute(static async (ILogger<THandler> logger, INext next) =>
            {
                try
                {
                    await next.RunAsync().ConfigureAwait(false);
                }
                catch(Exception x)
                {
                    logger?.LogUnhandledExceptionFromOnError(typeof(THandler), x);
                }
            })
            .Execute(static async (
                IEventHandler<TEvent> handler,
                TEvent @event,
                [ResolveFrom(PrimarySource = Source.Context, Fallback = false, PrimaryNotFound = NotFoundBehavior.ThrowException)]
                IRetryPolicy retryPolicy,
                [ResolveFrom(PrimarySource = Source.Context, Fallback = false, PrimaryNotFound = NotFoundBehavior.ThrowException)]
                CancellationToken ct) =>
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
                        PrimaryNotFound = NotFoundBehavior.ThrowException
                    }
                }
            })
            .Build();

        return new EventPipeline(typeof(TEvent), pipeline.Pipelines[0]);
    }
}
