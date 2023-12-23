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
    /// <returns>The task object representing the asynchronous operation.</returns>
    Task Publish<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : notnull;

    void Shutdown();
}
