using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace M.EventBrokerSlim.Internal;

internal class RetryQueue
{
    private readonly PriorityQueue<RetryDescriptor, long> _retryQueue;

    private readonly SemaphoreSlim _semaphore;
    private readonly ChannelWriter<object> _channelWriter;
    private readonly CancellationToken _cancellationToken;
    private bool _polling = false;

    public RetryQueue(ChannelWriter<object> channelWriter, CancellationToken cancellationToken)
    {
        _retryQueue = new PriorityQueue<RetryDescriptor, long>();
        _semaphore = new SemaphoreSlim(1, 1);
        _channelWriter = channelWriter;
        _cancellationToken = cancellationToken;
    }

    internal async Task Enqueue(RetryDescriptor retryDescriptor)
    {
        try
        {
            await _semaphore.WaitAsync(_cancellationToken).ConfigureAwait(false);
            _retryQueue.Enqueue(retryDescriptor, DateTime.UtcNow.Add(retryDescriptor.RetryPolicy.LastDelay).Ticks);
            if(!_polling)
            {
                _polling = true;
                _ = Task.Factory.StartNew(static async x => await Poll(x!).ConfigureAwait(false), this);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static async Task Poll(object state)
    {
        var self = (RetryQueue)state;
        while(true)
        {
            await self._semaphore.WaitAsync(self._cancellationToken).ConfigureAwait(false);

            while(self._retryQueue.TryPeek(out var retryDescriptor, out long ticks))
            {
                if(DateTime.UtcNow.Add(TimeSpan.FromMilliseconds(25)).Ticks >= ticks)
                {
                    await self._channelWriter.WriteAsync(retryDescriptor, self._cancellationToken).ConfigureAwait(false);
                    _ = self._retryQueue.Dequeue();
                }
                else
                {
                    break;
                }
            }

            if(self._retryQueue.Count == 0)
            {
                self._polling = false;
                self._semaphore.Release();
                return;
            }

            self._polling = true;
            self._semaphore.Release();
            await Task.Delay(TimeSpan.FromMilliseconds(25), self._cancellationToken).ConfigureAwait(false);
        }
    }
}
