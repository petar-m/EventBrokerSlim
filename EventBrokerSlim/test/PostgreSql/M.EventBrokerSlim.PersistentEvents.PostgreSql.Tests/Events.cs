using M.EventBrokerSlim.Persistent;

namespace PostgreSqlIntegrationTests;

public record SampleEvent(string Message);

public static class EventRegistryHelper
{
    private static readonly Lazy<EventRegistry> _registry =
        new(() => new EventRegistry()
                    .Add<SampleEvent>("sample-event"));

    public static EventRegistry Registry => _registry.Value;
}
