namespace M.EventBrokerSlim.Internal;

internal record EventBrokerSettings(int MaxConcurrentHandlers, bool DisableMissingHandlerWarningLog);
