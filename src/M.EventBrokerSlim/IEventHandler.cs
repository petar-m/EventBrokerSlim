using System;
using System.Threading;
using System.Threading.Tasks;
using M.EventBrokerSlim.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace M.EventBrokerSlim;

/// <summary>
/// Represents a logic for handling events.
/// </summary>
/// <typeparam name="TEvent">The type of the event.</typeparam>
public interface IEventHandler<TEvent>
{
    /// <summary>
    /// Handles the event.
    /// </summary>
    /// <param name="event">An instance of <typeparamref name="TEvent"/> representing the event.</param>
    /// <param name="retryPolicy">Provides ability to request a retry for the same event by the handler. Do not keep a reference to this instance, it may be pooled and reused.</param>
    /// <param name="cancellationToken">A cancellation token that should be used to cancel the work</param>
    Task Handle(TEvent @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken);

    /// <summary>
    /// Called when an unhadled exception is caught during execution.
    /// Exceptions thrown from this method are swallowed.
    /// If there is <see cref="ILogger"/> configured in the <see cref="IServiceCollection"/> an Error will be logged.
    /// </summary>
    /// <param name="exception">The exception caught.</param>
    /// <param name="event">The event instance which handling caused the exception.</param>
    /// <param name="retryPolicy">Provides ability to request a retry for the same event by the handler. Do not keep a reference to this instance, it may be pooled and reused.</param>
    /// <param name="cancellationToken">A cancellation token that should be used to cancel the work</param>
    Task OnError(Exception exception, TEvent @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken);
}
