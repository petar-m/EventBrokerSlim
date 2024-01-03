using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace M.EventBrokerSlim.Internal;

internal sealed class EventBroker : IEventBroker
{
    private readonly ChannelWriter<object> _channelWriter;

    public EventBroker(ChannelWriter<object> channelWriter)
    {
        _channelWriter = channelWriter;
    }

    public async Task Publish<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : notnull
    {
        ArgumentNullException.ThrowIfNull(@event, nameof(@event));
        try
        {
            await _channelWriter.WriteAsync(@event, cancellationToken);
        }
        catch (ChannelClosedException exception)
        {
            const string message = "EventBroker cannot publish event: Shutdown() has been called";
            throw new EventBrokerPublishNotAvailableException(message, exception);
        }
    }

    public Task PublishDeferred<TEvent>(TEvent @event, TimeSpan deferDuration) where TEvent : notnull
    {
        ArgumentNullException.ThrowIfNull(@event, nameof(@event));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(deferDuration.TotalMilliseconds, 0, nameof(deferDuration));

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(deferDuration).ConfigureAwait(false);
                await _channelWriter.WriteAsync(@event).ConfigureAwait(false);
            }
            catch
            {
                // intentional: do not allow unobserved exceptions
            }
        });

        return Task.CompletedTask;
    }

    public void Shutdown() => _ = _channelWriter.TryComplete();
}
