using System.Collections.Generic;
using FuncPipeline;

namespace M.EventBrokerSlim;

/// <summary>
/// Allows managing of delegate event handlers at runtime.
/// </summary>
public interface IDynamicEventHandlers
{
    /// <summary>
    /// Adds a pipeline handling specific event type.
    /// </summary>
    /// <typeparam name="TEvent">The type of the event.</typeparam>
    /// <param name="pipeline">The pipeline to handle the event.</param>
    /// <returns><see cref="IDynamicHandlerClaimTicket"/> identifying the added handler.</returns>
    IDynamicHandlerClaimTicket Add<TEvent>(IPipeline pipeline);

    /// <summary>
    /// Removes a handler pipeline identified by the given claim ticket.
    /// </summary>
    /// <param name="claimTicket">The <see cref="IDynamicHandlerClaimTicket"/> identifying the handler to remove.</param>
    void Remove(IDynamicHandlerClaimTicket claimTicket);

    /// <summary>
    /// Removes multiple handler pipelines identified by the given claim tickets.
    /// </summary>
    /// <param name="claimTickets">A collection of <see cref="IDynamicHandlerClaimTicket"/> identifying the handlers to remove.</param>
    void RemoveRange(IEnumerable<IDynamicHandlerClaimTicket> claimTickets);
}
