using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace M.EventBrokerSlim.Tests;

public class Orchestrator<T, TEvent> : IEventHandler<TEvent>
    where TEvent : ITraceable<T>
{
    private readonly ConcurrentDictionary<T, object> _expected = new();
    private readonly TimeSpan _waitForItemsTimeout = TimeSpan.FromMilliseconds(10);
    private readonly ConcurrentBag<Exception> _exceptions = new();

    public void Expect(params ITraceable<T>[] items)
    {
        foreach (var item in items)
        {
            _expected.TryAdd(item.CorrelationId, null);
        }
    }

     public async Task<bool> WaitForExpected(TimeSpan timeout = default)
    {
        var deadline = timeout == default ? DateTime.MaxValue : DateTime.UtcNow + timeout;

        while (DateTime.UtcNow <= deadline)
        {
            if (_expected.IsEmpty)
            {
                return true;
            }

            await Task.Delay(_waitForItemsTimeout);
        }

        return false;
    }

    public async Task Wait(TimeSpan timeout) => await Task.Delay(timeout);

    public virtual Task Handle(TEvent @event)
    {
        _expected.TryRemove(@event.CorrelationId, out _);
        return Task.CompletedTask;
    }

    public virtual Task OnError(Exception exception, TEvent @event)
    {
        _exceptions.Add(exception);
        return Task.CompletedTask;
    }

    public Exception[] Exceptions => _exceptions.ToArray();
}
