using System;
using System.Threading.Tasks;
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
    Task Handle(TEvent @event);

    /// <summary>
    /// Called when an unhadled exception is caught during execution.
    /// Exceptions thrown from this method are swalloled.
    /// If there is <see cref="ILogger"/> configured in the <see cref="IServiceCollection"/> an Error will be logged.
    /// </summary>
    /// <param name="exception">The exception caught.</param>
    /// <param name="event">The event instance which handling caused the exception.</param>
    Task OnError(Exception exception, TEvent @event);
}
