using M.EventBrokerSlim.Persistent;

namespace LiteDbIntegrationTests;

public record SampleEvent(string Message);

public record SampleEvent2(int Value, string Description);

public static class EventRegistryHelper
{
    private static readonly Lazy<EventRegistry> _registry =
        new(() => new EventRegistry()
                    .Add<SampleEvent>("sample-event")
                    .Add<SampleEvent2>("sample-event-2"));

    public static EventRegistry Registry => _registry.Value;
}
