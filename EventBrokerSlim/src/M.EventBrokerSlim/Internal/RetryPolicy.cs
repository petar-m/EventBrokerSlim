using System;

namespace M.EventBrokerSlim.Internal;

internal class RetryPolicy : IRetryPolicy
{
    private TimeSpan _delay;

    internal RetryPolicy()
    {
    }

    public void RetryAfter(TimeSpan delay)
    {
        _delay = delay;
        RetryRequested = true;
    }

    public void RetryAfter(Func<uint, TimeSpan, TimeSpan> delay)
    {
        _delay = delay(Attempt, _delay);
        RetryRequested = true;
    }

    public uint Attempt { get; private set; }

    public TimeSpan LastDelay => _delay;

    public bool RetryRequested { get; private set; }

    internal void NextAttempt()
    {
        Attempt++;
        RetryRequested = false;
    }

    internal void Clear()
    {
        Attempt = 0;
        _delay = TimeSpan.Zero;
        RetryRequested = false;
    }
}
