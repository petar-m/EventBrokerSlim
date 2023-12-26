using System;

namespace M.EventBrokerSlim;

#pragma warning disable RCS1194 // Implement exception constructors
public sealed class EventBrokerPublishNotAvailableException : Exception
{
    public EventBrokerPublishNotAvailableException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
#pragma warning restore RCS1194 // Implement exception constructors
