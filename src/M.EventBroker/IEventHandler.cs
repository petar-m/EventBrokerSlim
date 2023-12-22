﻿using System;
using System.Threading.Tasks;

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
    /// <param name="event">An instance of TEvent representing the event.</param>
    Task Handle(TEvent @event);

    /// <summary>
    /// Called when an error is caught during execution.
    /// </summary>
    /// <param name="exception">The exception caught.</param>
    /// <param name="event">The event instance which handling caused the exception.</param>
    Task OnError(Exception exception, TEvent @event);
}
