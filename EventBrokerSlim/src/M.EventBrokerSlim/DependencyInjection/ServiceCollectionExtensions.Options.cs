using System;
using System.Diagnostics.CodeAnalysis;
using FuncPipeline;
using Microsoft.Extensions.DependencyInjection;

namespace M.EventBrokerSlim.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
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
    /// <param name="configure">
    /// A delegate to configure the <see cref="EventHandlerOptions"/>.
    /// </param>
    /// <returns>
    /// The <see cref="IServiceCollection"/> so that additional calls can be chained.
    /// </returns>
    public static IServiceCollection AddScopedEventHandler<TEvent, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(
        this IServiceCollection services,
        Action<EventHandlerOptions> configure)
        where THandler : class, IEventHandler<TEvent>
    {
        var options = new EventHandlerOptions();
        configure(options);
        return services.AddScopedEventHandler<TEvent, THandler>(eventHandlerKey: options.ServiceKey, eventBrokerKey: options.EventBrokerKey, handlerName: options.HandlerName);
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
    /// <param name="configure">
    /// A delegate to configure the <see cref="EventHandlerOptions"/>.
    /// </param>
    /// <returns>
    /// The <see cref="IServiceCollection"/> so that additional calls can be chained.
    /// </returns>
    public static IServiceCollection AddSingletonEventHandler<TEvent, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(
        this IServiceCollection services,
        Action<EventHandlerOptions> configure)
        where THandler : class, IEventHandler<TEvent>
    {
        var options = new EventHandlerOptions();
        configure(options);
        return services.AddSingletonEventHandler<TEvent, THandler>(eventHandlerKey: options.ServiceKey, eventBrokerKey: options.EventBrokerKey, handlerName: options.HandlerName);
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
    /// <param name="configure">
    /// A delegate to configure the <see cref="EventHandlerOptions"/>.
    /// </param>
    /// <returns>
    /// The <see cref="IServiceCollection"/> so that additional calls can be chained.
    /// </returns>
    public static IServiceCollection AddTransientEventHandler<TEvent, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(
        this IServiceCollection services,
        Action<EventHandlerOptions> configure)
        where THandler : class, IEventHandler<TEvent>
    {
        var options = new EventHandlerOptions();
        configure(options);
        return services.AddTransientEventHandler<TEvent, THandler>(eventHandlerKey: options.ServiceKey, eventBrokerKey: options.EventBrokerKey, handlerName: options.HandlerName);
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
    /// <param name="configure">
    /// A delegate to configure the <see cref="PipelineHandlerOptions"/>.
    /// </param>
    /// <returns>
    /// The <see cref="IServiceCollection"/> so that additional calls can be chained.
    /// </returns>
    public static IServiceCollection AddEventHandlerPipeline<TEvent>(
        this IServiceCollection services,
        IPipeline pipeline,
        Action<PipelineHandlerOptions> configure)
    {
        var options = new PipelineHandlerOptions();
        configure(options);
        return services.AddEventHandlerPipeline<TEvent>(pipeline, eventBrokerKey: options.EventBrokerKey, handlerName: options.HandlerName);
    }
}
