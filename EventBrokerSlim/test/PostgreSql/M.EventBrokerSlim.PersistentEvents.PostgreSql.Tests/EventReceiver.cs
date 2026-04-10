using System.Collections.Concurrent;
using M.EventBrokerSlim.Persistent;

namespace PostgreSqlIntegrationTests;

public class EventReceiver
{
    private readonly ConcurrentBag<ReceivedEvent> _receivedEvents = new();

    public void Add(EventRecord record) => _receivedEvents.Add(new ReceivedEvent(record.DeserializedEvent, record, DateTime.UtcNow));

    public async Task<TEvent> WaitForSingleAsync<TEvent>(TimeSpan timeout, CancellationToken cancellationToken) where TEvent : class
    {
        var deadline = DateTime.UtcNow + timeout;
        var wait = TimeSpan.FromMilliseconds(100);
        do
        {
            TEvent? e = _receivedEvents.SingleOrDefault(e => e.Event is TEvent)?.Event as TEvent;
            if(e != null)
            {
                return e;
            }

            await Task.Delay(wait, cancellationToken);
        }
        while(DateTime.UtcNow <= deadline);

        throw new TimeoutException($"Timeout waiting for event of type {typeof(TEvent).Name}");
    }

    public record ReceivedEvent(object Event, EventRecord EventRecord, DateTime ReceivedAt);
}
