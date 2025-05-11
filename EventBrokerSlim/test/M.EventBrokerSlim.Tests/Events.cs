namespace M.EventBrokerSlim.Tests;

public record TestEventBase(int Number);

public record Event1(int Number) : TestEventBase(Number);

public record Event2(int Number) : TestEventBase(Number);

public record Event3(int Number) : TestEventBase(Number);
