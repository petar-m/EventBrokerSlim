using System;
using System.Collections.Generic;

namespace M.EventBrokerSlim.Internal;

internal sealed class DelegateHandlerDescriptor
{
    public required Type EventType { get; init; }

    public required Type[] ParamTypes { get; init; }

    public required object Handler { get; init; }

    public List<DelegateHandlerDescriptor> Pipeline { get; } = new();

    internal DynamicHandlerClaimTicket? ClaimTicket { get; set; }
}
