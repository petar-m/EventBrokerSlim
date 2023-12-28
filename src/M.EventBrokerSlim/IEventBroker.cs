using System.Threading;
using System.Threading.Tasks;

namespace M.EventBrokerSlim;

/// <summary>
/// Represents an event broker.
/// </summary>
public interface IEventBroker
{
    /// <summary>
    /// Publishes an event of type <typeparamref name="TEvent"/>.
    /// </summary>
    /// <typeparam name="TEvent">The type of the event.</typeparam>
    /// <param name="event">An <typeparamref name="TEvent"/> instance to be passed to all handlers of the event.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the publish operation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="EventBrokerPublishNotAvailableException">When called after Shutdown()</exception>
    /// <exception cref="System.ArgumentNullException"></exception>
    Task Publish<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : notnull;

    /// <summary>
    /// Any further attempt to publish event will result in exception.
    /// </summary>
    void Shutdown();
}
