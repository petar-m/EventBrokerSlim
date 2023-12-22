using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace M.EventBrokerSlim.Tests;

public class Orchestrator<T, TEvent> : IEventHandler<TEvent>
    where TEvent : ITraceableEvent<T>, IIdentifieableEvent<T>
{
    private readonly ConcurrentDictionary<T, object> _expected = new();
    private readonly TimeSpan _waitForItemsTimeout = TimeSpan.FromMilliseconds(10);
    private readonly ConcurrentBag<Exception> _exceptions = new();

    private T _correlationId;

    public void Begin(T correlationId)
    {
        _correlationId = correlationId;
    }

    public void Expect(IEnumerable<IIdentifieableEvent<T>> items)
    {
        foreach (var item in items)
        {
            _expected.TryAdd(item.Id, null);
        }
    }

    public void Expect(IIdentifieableEvent<T> item)
    {
        _expected.TryAdd(item.Id, null);
    }

    public async Task<bool> Complete(TimeSpan timeout = default)
    {
        var deadline = timeout == default ? DateTime.MaxValue : DateTime.UtcNow + timeout;

        while (DateTime.UtcNow <= deadline)
        {
            if (_expected.Count == 0)
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
        if (!@event.CorrelationId.Equals(_correlationId))
        {
            return Task.CompletedTask;
        }

        _expected.TryRemove(@event.Id, out _);
        return Task.CompletedTask;
    }

    public Task OnError(Exception exception, TEvent @event)
    {
        _exceptions.Add(exception);
        return Task.CompletedTask;
    }

    public Exception[] Exceptions => _exceptions.ToArray();
}
