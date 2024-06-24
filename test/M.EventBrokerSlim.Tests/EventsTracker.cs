using System.Collections.Concurrent;
using System.Diagnostics;

namespace M.EventBrokerSlim.Tests;

public class EventsTracker
{
    private readonly Stopwatch _stopwatch = new Stopwatch();
    private CancellationTokenSource? _cancellationTokenSource;

    public int ExpectedItemsCount { get; set; } = int.MaxValue;

    public TimeSpan Elapsed => _stopwatch.Elapsed;

    public void Track(object item)
    {
        Items.Add((item, DateTime.UtcNow));
        if(Items.Count == ExpectedItemsCount && _cancellationTokenSource is not null)
        {
            _cancellationTokenSource.Cancel();
            _stopwatch.Stop();
        }
    }

    public Task TrackAsync(object item)
    {
        Track(item);
        return Task.CompletedTask;
    }

    public ConcurrentBag<(object Item, DateTime Timestamp)> Items { get; } = new();

    public async Task Wait(TimeSpan timeout)
    {
        if(Items.Count == ExpectedItemsCount)
        {
            return;
        }

        _stopwatch.Start();
        _cancellationTokenSource = new CancellationTokenSource(timeout);
        try
        {
            await Task.Delay(timeout, _cancellationTokenSource.Token);
        }
        catch(TaskCanceledException)
        {
        }
        finally
        {
            _stopwatch.Stop();
        }
    }
}
