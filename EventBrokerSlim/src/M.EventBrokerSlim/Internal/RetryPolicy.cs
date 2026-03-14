using System;
using M.EventBrokerSlim;
using Microsoft.Extensions.ObjectPool;

internal class RetryPolicy : IRetryPolicy, IResettable
{
    internal RetryPolicy()
    {
    }

    public void RetryAfter(TimeSpan delay)
    {
        LastDelay = delay;
        RetryRequested = true;
    }

    public void RetryAfter(Func<uint, TimeSpan, TimeSpan> delay)
    {
        LastDelay = delay(Attempt, LastDelay);
        RetryRequested = true;
    }

    public uint Attempt { get; internal set; }

    public TimeSpan LastDelay { get; internal set; }

    public bool RetryRequested { get; private set; }

    internal void NextAttempt()
    {
        Attempt++;
        RetryRequested = false;
    }

    public bool TryReset()
    {
        Attempt = 0;
        LastDelay = TimeSpan.Zero;
        RetryRequested = false;
        return true;
    }
}
