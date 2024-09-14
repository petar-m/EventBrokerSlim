using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using M.EventBrokerSlim.DependencyInjection;

namespace M.EventBrokerSlim.Internal;

internal sealed class DynamicEventHandlers : IDynamicEventHandlers
{
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
    private readonly Dictionary<Type, ImmutableList<DelegateHandlerDescriptor>> _handlers = new Dictionary<Type, ImmutableList<DelegateHandlerDescriptor>>();

    public IDynamicHandlerClaimTicket Add(DelegateHandlerRegistryBuilder builder)
    {
        try
        {
            _semaphore.Wait();
            var claimTicket = new DynamicHandlerClaimTicket(Guid.NewGuid());
            foreach(DelegateHandlerDescriptor handler in builder.HandlerDescriptors)
            {
                if(!_handlers.TryGetValue(handler.EventType, out ImmutableList<DelegateHandlerDescriptor>? value))
                {
                    value = ImmutableList<DelegateHandlerDescriptor>.Empty;
                    _handlers[handler.EventType] = value;
                }

                handler.ClaimTicket = claimTicket;
                _handlers[handler.EventType] = value.Add(handler);
            }

            return claimTicket;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Remove(IDynamicHandlerClaimTicket claimTicket)
    {
        try
        {
            _semaphore.Wait();
            foreach(var key in _handlers.Keys)
            {
                _handlers[key] = _handlers[key].RemoveAll(x =>
                {
                    if(x.ClaimTicket is null)
                    {
                        return true;
                    }

                    if(x.ClaimTicket.Equals(claimTicket))
                    {
                        x.ClaimTicket = null;
                        return true;
                    }

                    return false;
                });
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void RemoveRange(IEnumerable<IDynamicHandlerClaimTicket> claimTickets)
    {
        try
        {
            _semaphore.Wait();
            var claimTicketSet = new HashSet<IDynamicHandlerClaimTicket>(claimTickets);
            foreach(var key in _handlers.Keys)
            {
                _handlers[key] = _handlers[key].RemoveAll(x =>
                {
                    if(x.ClaimTicket is null)
                    {
                        return true;
                    }

                    if(claimTicketSet.Contains(x.ClaimTicket))
                    {
                        x.ClaimTicket = null;
                        return true;
                    }

                    return false;
                });
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    internal ImmutableList<DelegateHandlerDescriptor>? GetDelegateHandlerDescriptors(Type eventType)
    {
        _ = _handlers.TryGetValue(eventType, out ImmutableList<DelegateHandlerDescriptor>? handlerDescriptors);
        return handlerDescriptors;
    }
}
