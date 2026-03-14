using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace M.EventBrokerSlim.Internal.Persistent;

/// <summary>
/// A signal primitive for a single-consumer polling pattern, backed by a Channel.
///
/// Thread safety:
///   - SendAsync()       : safe to call concurrently from multiple threads/tasks
///   - All other methods : must be called from a single consumer (not concurrently)
/// </summary>
internal sealed class PollRequiredSignal
{
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleReader = true,
        SingleWriter = false
    });

    public void Send()
    {
        // TryWrite is sufficient: DropOldest policy handles concurrent signals.
        _channel.Writer.TryWrite(true);
    }

    public void Reset()
    {
        // discard any existing signal
        _channel.Reader.TryRead(out _);
    }

    /// <summary>
    /// Waits until signaled or timeout/cancellation occurs. Consumer-only.
    /// Returns true if signaled, false if timed out.
    /// Throws OperationCanceledException if cancellationToken is cancelled.
    /// </summary>
    public async Task WaitForSignalAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        try
        {
            await _channel.Reader.WaitToReadAsync(timeoutCts.Token).ConfigureAwait(false);
            _channel.Reader.TryRead(out _); // consume
        }
        catch(OperationCanceledException) when(!cancellationToken.IsCancellationRequested)
        {
            // timeout occurred
        }
    }
}
