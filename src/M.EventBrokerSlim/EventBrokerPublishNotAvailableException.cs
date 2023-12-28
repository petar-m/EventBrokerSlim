using System;

namespace M.EventBrokerSlim;

#pragma warning disable RCS1194 // Implement exception constructors
/// <summary>
/// The exception that is thrown when a <see cref="IEventBroker.Publish{TEvent}(TEvent, System.Threading.CancellationToken)"/> is called after <see cref="IEventBroker.Shutdown"></see>.
/// </summary>
public sealed class EventBrokerPublishNotAvailableException : Exception
{
    internal EventBrokerPublishNotAvailableException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
#pragma warning restore RCS1194 // Implement exception constructors
