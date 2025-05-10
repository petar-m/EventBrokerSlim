using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using FuncPipeline;
using Microsoft.Extensions.DependencyInjection;

namespace M.EventBrokerSlim.Internal;

internal sealed class DynamicEventHandlers : IDynamicEventHandlers
{
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
    private readonly Dictionary<Type, ImmutableList<(DynamicHandlerClaimTicket ticket, IPipeline pipeline)>> _handlers = new();
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public DynamicEventHandlers(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    public IDynamicHandlerClaimTicket Add<TEvent>(IPipeline pipeline)
    {
        var eventType = typeof(TEvent);
        var claimTicket = new DynamicHandlerClaimTicket(Guid.NewGuid(), eventType);
        pipeline.ServiceScopeFactory ??= _serviceScopeFactory;
        _semaphore.Wait();
        try
        {
            if(!_handlers.TryGetValue(eventType, out ImmutableList<(DynamicHandlerClaimTicket, IPipeline)>? eventHandlersList))
            {
                eventHandlersList = ImmutableList<(DynamicHandlerClaimTicket, IPipeline)>.Empty;
                _handlers.Add(eventType, eventHandlersList);
            }

            _handlers[eventType] = eventHandlersList.Add((claimTicket, pipeline));
            return claimTicket;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Remove(IDynamicHandlerClaimTicket claimTicket)
    {
        ArgumentNullException.ThrowIfNull(claimTicket);
        var ticket = claimTicket as DynamicHandlerClaimTicket;
        if(ticket is null)
        {
            return;
        }

        _semaphore.Wait();
        try
        {
            if(!_handlers.TryGetValue(ticket.EventType, out ImmutableList<(DynamicHandlerClaimTicket, IPipeline)>? eventHandlersList))
            {
                return;
            }

            _handlers[ticket.EventType] = _handlers[ticket.EventType].RemoveAll(x => x.ticket.Id == ticket.Id);

        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void RemoveRange(IEnumerable<IDynamicHandlerClaimTicket> claimTickets)
    {
        ArgumentNullException.ThrowIfNull(claimTickets);
        if(!claimTickets.All(x => x is DynamicHandlerClaimTicket))
        {
            return;
        }

        _semaphore.Wait();
        try
        {
            foreach(var ticket in claimTickets.Cast<DynamicHandlerClaimTicket>())
            {
                if(!_handlers.TryGetValue(ticket.EventType, out ImmutableList<(DynamicHandlerClaimTicket, IPipeline)>? eventHandlersList))
                {
                    continue;
                }

                _handlers[ticket.EventType] = _handlers[ticket.EventType].RemoveAll(x => x.ticket.Id == ticket.Id);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    internal ImmutableList<(DynamicHandlerClaimTicket ticket, IPipeline pipeline)>? GetDelegateHandlerDescriptors(Type eventType)
    {
        _semaphore.Wait();
        try
        {
            _ = _handlers.TryGetValue(eventType, out ImmutableList<(DynamicHandlerClaimTicket ticket, IPipeline pipeline)>? handlers);
            return handlers;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
