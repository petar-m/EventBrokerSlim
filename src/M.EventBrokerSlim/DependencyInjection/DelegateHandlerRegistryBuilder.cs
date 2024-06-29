using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using M.EventBrokerSlim.Internal;

namespace M.EventBrokerSlim.DependencyInjection;

/// <summary>
/// Used to define event handlers as delegates.
/// </summary>
public class DelegateHandlerRegistryBuilder
{
    /// <summary>
    /// Registers an event handler delegate returning <see cref="Task"/>.
    /// All of the parameters will be resolved from new DI container scope and injected. The scope will be disposed after delegate execution.
    /// <br/>Tip: Use <see langword="static"/> keyword for the anonymous delegate to avoid accidential closure.
    /// </summary>
    /// <remarks>
    /// Special objects available (without being registered in DI container):
    /// <list type="bullet">
    /// <item><typeparamref name="TEvent"/> - an instance of the event being handled.</item>
    /// <item><see cref="CancellationToken"/> - a cancellation token that should be used to cancel the work.</item>
    /// <item><see cref="IRetryPolicy" /> - provides ability to request a retry for the same event by the handler. Do not keep a reference to this instance, it may be pooled and reused</item>
    /// </list>
    /// </remarks>
    /// <typeparam name="TEvent">The type of the event being handled.</typeparam>
    /// <param name="handler">A delegate returning <see cref="Task"/> that will be executed when event of type <typeparamref name="TEvent"/> is published.</param>
    /// <returns>Object allowing to fluently continue registering handlers or build handler pipeline.</returns>
    /// <exception cref="InvalidOperationException">Thrown when registry is closed for new registrations. Usually after an instance of <see cref="IEventBroker"/> has been resolved.</exception>
    public DelegateHandlerWrapperBuilder RegisterHandler<TEvent>(Delegate handler)
    {
        if(IsClosed)
        {
            throw new InvalidOperationException("Registry is closed. Please complete registrations before IEventBroker is resolved.");
        }

        var descriptor = DelegateHelper.BuildDelegateHandlerDescriptor(handler, typeof(TEvent));
        HandlerDescriptors.Add(descriptor);
        return new DelegateHandlerWrapperBuilder(this, descriptor);
    }

    /// <summary>
    /// Indicates whether the registry can still be used to register event handlers.
    /// </summary>
    public bool IsClosed { get; private set; }

    internal List<DelegateHandlerDescriptor> HandlerDescriptors { get; } = new();

    internal static DelegateHandlerRegistry Build(IEnumerable<DelegateHandlerRegistryBuilder> builders)
    {
        return new DelegateHandlerRegistry(
            builders.SelectMany(x =>
            {
                x.IsClosed = true;
                return x.HandlerDescriptors;
            }));
    }

    /// <summary>
    /// Used to define pipeline of delegates wrapping the event handler.
    /// </summary>
    /// <remarks>
    /// Order of execution is in reverse of registration order. The last registered is executed first, moving "inwards" toward the handler.
    /// </remarks>
    public class DelegateHandlerWrapperBuilder
    {
        private readonly DelegateHandlerRegistryBuilder _builder;
        private readonly DelegateHandlerDescriptor _handlerDescriptor;

        internal DelegateHandlerWrapperBuilder(DelegateHandlerRegistryBuilder builder, DelegateHandlerDescriptor handlerDescriptor)
        {
            _builder = builder;
            _handlerDescriptor = handlerDescriptor;
        }

        /// <summary>
        /// Returns the current <see cref="DelegateHandlerRegistryBuilder"/> instance, allowing to continue registering event handlers.
        /// </summary>
        /// <returns>Current <see cref="DelegateHandlerRegistryBuilder"/> instance.</returns>
        public DelegateHandlerRegistryBuilder Builder() => _builder;

        /// <summary>
        /// Registers a delegate returning <see cref="Task"/> executed before the event handler delegate. Use <see cref="INextHandler.Execute"/> to call the next wrapper in the chain.
        /// All of the parameters will be resolved from new DI container scope and injected. All wrappers and the handler share the same DI container scope. Order of execution is in reverse of registration order. The last registered is executed first, moving "inwards" toward the handler.
        /// <br/>Tip: Use <see langword="static"/> keyword for the anonymous delegate to avoid accidential closure.
        /// </summary>
        /// <remarks>
        /// Special objects available (without being registered in DI container):
        /// <list type="bullet">
        /// <item><see cref="INextHandler" /> - used to call the next wrapper in the chain or the handler if no more wrappers available. Do not keep a reference to this instance, it may be pooled and reused</item>
        /// <item>TEvent - an instance of the event being handled.</item>
        /// <item><see cref="CancellationToken"/> - a cancellation token that should be used to cancel the work.</item>
        /// <item><see cref="IRetryPolicy" /> - provides ability to request a retry for the same event by the handler. Do not keep a reference to this instance, it may be pooled and reused</item>
        /// </list>
        /// </remarks>
        /// <param name="wrapper">A delegate returning <see cref="Task"/> that will be executed when event of type TEvent is published, before the handler delegate.</param>
        /// <returns>Object allowing to fluently continue registering handlers or build handler pipeline.</returns>
        /// <exception cref="InvalidOperationException">Thrown when registry is closed for new registrations. Usually after an instance of <see cref="IEventBroker"/> has been resolved.</exception>
        public DelegateHandlerWrapperBuilder WrapWith(Delegate wrapper)
        {
            if(_builder.IsClosed)
            {
                throw new InvalidOperationException("Registry is closed. Please complete registrations before IEventBroker is resolved.");
            }

            var descriptor = DelegateHelper.BuildDelegateHandlerDescriptor(wrapper, _handlerDescriptor.EventType);
            _handlerDescriptor.Pipeline.Add(descriptor);
            return this;
        }
    }
}
