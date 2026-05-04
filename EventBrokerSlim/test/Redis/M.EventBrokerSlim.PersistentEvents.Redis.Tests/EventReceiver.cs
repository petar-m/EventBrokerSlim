using System.Collections.Concurrent;
using M.EventBrokerSlim.Persistent;

namespace RedisIntegrationTests;

public class EventReceiver
{
    private readonly ConcurrentBag<ReceivedEvent> _receivedEvents = new();

    public void Add(EventRecord record) => _receivedEvents.Add(new ReceivedEvent(record.DeserializedEvent, record, DateTime.UtcNow));

    public List<ReceivedEvent> GetReceivedEvents() => _receivedEvents.ToList();

    public async Task WaitForEventsAsync(int count, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        var wait = TimeSpan.FromMilliseconds(100);
        do
        {
            if(_receivedEvents.Count > count)
            {
                throw new Exception($"Expected {count} events, but received {_receivedEvents.Count}");
            }

            if(_receivedEvents.Count == count)
            {
                return;
            }

            await Task.Delay(wait, cancellationToken);
        }
        while(DateTime.UtcNow <= deadline);

        throw new TimeoutException($"Expected {count} events, but received {_receivedEvents.Count} within {timeout}");
    }

    public record ReceivedEvent(object Event, EventRecord EventRecord, DateTime ReceivedAt);
}
