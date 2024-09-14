using System;

namespace M.EventBrokerSlim.Internal;

internal sealed record DynamicHandlerClaimTicket(Guid Id) : IDynamicHandlerClaimTicket;
