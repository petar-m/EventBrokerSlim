using System;
using Microsoft.Extensions.Logging;

namespace M.EventBrokerSlim.Internal;

internal static partial class LogMessages
{

    [LoggerMessage(
        Message = "Unhandled exception executing {serviceType}.OnError()",
        Level = LogLevel.Error)]
    internal static partial void LogUnhandledExceptionFromOnError(
        this ILogger logger, Type serviceType, Exception exception);

    [LoggerMessage(
        Message = "No event handler found for event {eventType}",
        Level = LogLevel.Warning)]
    internal static partial void LogNoEventHandlerForEvent(this ILogger logger, Type eventType);

    [LoggerMessage(
        Message = "Unhandled exception executing handler for event {eventType}",
        Level = LogLevel.Error)]
    internal static partial void LogDelegateEventHandlerError(this ILogger logger, Type eventType, Exception exception);
}
