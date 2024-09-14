using System.Collections.Generic;
using M.EventBrokerSlim.DependencyInjection;

namespace M.EventBrokerSlim;

/// <summary>
/// Allows managing of delegate event handlers at runtime.
/// </summary>
public interface IDynamicEventHandlers
{
    /// <summary>
    /// Adds one or more delegate handlers.
    /// </summary>
    /// <param name="builder">An instance of <see cref="DelegateHandlerRegistryBuilder"/> describing the handlers.</param>
    /// <returns><see cref="IDynamicHandlerClaimTicket"/> identifying added handlers.</returns>
    IDynamicHandlerClaimTicket Add(DelegateHandlerRegistryBuilder builder);

    /// <summary>
    /// Removes one or more delegate handlers by <see cref="IDynamicHandlerClaimTicket"/>
    /// </summary>
    /// <param name="claimTicket"><see cref="IDynamicHandlerClaimTicket"/> identifying handlers to remove.</param>
    void Remove(IDynamicHandlerClaimTicket claimTicket);

    /// <summary>
    /// Removes one or more delegate handlers by <see cref="IDynamicHandlerClaimTicket"/>
    /// </summary>
    /// <param name="claimTickets">Multiple <see cref="IDynamicHandlerClaimTicket"/> identifying handlers to remove.</param>
    void RemoveRange(IEnumerable<IDynamicHandlerClaimTicket> claimTickets);
}
