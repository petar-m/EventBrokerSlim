namespace M.EventBrokerSlim.DependencyInjection;

internal record EventBrokerSettings(int MaxConcurrentHandlers, bool DisableMissingHandlerWarningLog);
