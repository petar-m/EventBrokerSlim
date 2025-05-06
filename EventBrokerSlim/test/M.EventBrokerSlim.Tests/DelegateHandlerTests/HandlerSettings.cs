namespace M.EventBrokerSlim.Tests.DelegateHandlerTests;

public record HandlerSettings(int RetryAttempts, TimeSpan Delay);
