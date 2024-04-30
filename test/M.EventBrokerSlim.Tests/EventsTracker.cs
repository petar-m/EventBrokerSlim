using System.Collections.Concurrent;

namespace M.EventBrokerSlim.Tests;

public class EventsTracker
{
    public void Track(object @event)
    {
        Items.Add((@event, DateTime.UtcNow));
    }

    public ConcurrentBag<(object Event, DateTime Timestamp)> Items { get; } = new();

    public async Task Wait(TimeSpan timeout)
    {
        await Task.Delay(timeout);
    }
}
