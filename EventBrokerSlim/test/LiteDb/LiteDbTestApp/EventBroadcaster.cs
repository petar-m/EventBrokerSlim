using System.Threading.Channels;

namespace LiteDbTestApp;

public record ProcessedEvent(string Message, DateTimeOffset PublishedAt, DateTimeOffset HandledAt);

public class EventBroadcaster
{
    private readonly object _lock = new();
    private readonly List<Channel<ProcessedEvent>> _subscribers = [];

    public IDisposable Subscribe(out ChannelReader<ProcessedEvent> reader)
    {
        var channel = Channel.CreateBounded<ProcessedEvent>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

        lock(_lock)
        {
            _subscribers.Add(channel);
        }

        reader = channel.Reader;
        return new Subscription(this, channel);
    }

    public void Broadcast(ProcessedEvent processedEvent)
    {
        lock(_lock)
        {
            foreach(var channel in _subscribers)
            {
                channel.Writer.TryWrite(processedEvent);
            }
        }
    }

    private void Unsubscribe(Channel<ProcessedEvent> channel)
    {
        lock(_lock)
        {
            _subscribers.Remove(channel);
        }

        channel.Writer.TryComplete();
    }

    private sealed class Subscription(EventBroadcaster broadcaster, Channel<ProcessedEvent> channel) : IDisposable
    {
        public void Dispose() => broadcaster.Unsubscribe(channel);
    }
}
