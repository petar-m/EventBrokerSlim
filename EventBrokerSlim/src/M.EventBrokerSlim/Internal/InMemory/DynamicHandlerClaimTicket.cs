using System;

namespace M.EventBrokerSlim.Internal.InMemory;

internal sealed record DynamicHandlerClaimTicket(Guid Id, Type EventType) : IDynamicHandlerClaimTicket;
