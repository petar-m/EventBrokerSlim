using System.Collections.Concurrent;

namespace M.EventBrokerSlim.Tests;

public class EventsRecorder<T> where T : notnull
{
    private readonly ConcurrentDictionary<T, object?> _expected = new();
    private readonly TimeSpan _waitForItemsTimeout = TimeSpan.FromMilliseconds(20);
    private readonly ConcurrentBag<Exception> _exceptions = [];
    private readonly ConcurrentBag<(T id, long tick)> _events = [];
    private readonly ConcurrentBag<(int id, long tick)> _handlerInstances = [];
    private readonly ConcurrentBag<(int id, long tick)> _scopeInstances = [];

    public Exception[] Exceptions => _exceptions.ToArray();

    public T[] HandledEventIds => _events.OrderBy(x => x.tick).Select(x => x.id).ToArray();

    public T[] Expected => _expected.Keys.ToArray();

    public int[] HandlerObjectsHashCodes => _handlerInstances.OrderBy(x => x.tick).Select(x => x.id).ToArray();

    public int[] HandlerScopeHashCodes => _scopeInstances.OrderBy(x => x.tick).Select(x => x.id).ToArray();

    public void Expect(params ITraceable<T>[] items)
    {
        foreach(var item in items)
        {
            _expected.TryAdd(item.CorrelationId, null);
        }
    }

    public void Expect(params T[] items)
    {
        foreach(var item in items)
        {
            _expected.TryAdd(item, null);
        }
    }

    public async Task<bool> WaitForExpected(TimeSpan timeout = default)
    {
        var deadline = timeout == default ? DateTime.MaxValue : DateTime.UtcNow + timeout;

        while(DateTime.UtcNow <= deadline)
        {
            if(_expected.IsEmpty)
            {
                return true;
            }

            await Task.Delay(_waitForItemsTimeout);
        }

        return false;
    }

    public async Task Wait(TimeSpan timeout) => await Task.Delay(timeout);

    public virtual void Notify(ITraceable<T> @event, int? handlerInstance = null, int? scopeInstance = null)
    {
        _events.Add((@event.CorrelationId, DateTime.UtcNow.Ticks));

        if(handlerInstance.HasValue)
        {
            _handlerInstances.Add((id: handlerInstance.Value, tick: DateTime.UtcNow.Ticks));
        }

        if(scopeInstance.HasValue)
        {
            _scopeInstances.Add((id: scopeInstance.Value, tick: DateTime.UtcNow.Ticks));
        }

        _expected.TryRemove(@event.CorrelationId, out _);
    }

    public virtual void Notify(T correlationId)
    {
        _events.Add((correlationId, DateTime.UtcNow.Ticks));
        _expected.TryRemove(correlationId, out _);
    }

    public virtual void Notify(Exception exception, ITraceable<T> @event)
    {
        _exceptions.Add(exception);
    }
}
