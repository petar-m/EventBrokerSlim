using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace M.EventBrokerSlim.Internal;

/// <summary>
/// Manages event subscriptions and invoking of event handlers.
/// </summary>
internal class EventBroker : IEventBroker
{
    private readonly ChannelWriter<object> _channelWriter;

    public EventBroker(ChannelWriter<object> channelWriter)
    {
        _channelWriter = channelWriter;
    }

    public async Task Publish<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : notnull
    {
        ArgumentNullException.ThrowIfNull(@event, nameof(@event));
        await _channelWriter.WriteAsync(@event, cancellationToken);
    }

    public void Shutdown()
    {
        _channelWriter.Complete();
    }
}
