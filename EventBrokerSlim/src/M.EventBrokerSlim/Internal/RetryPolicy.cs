using System;
using M.EventBrokerSlim;
using Microsoft.Extensions.ObjectPool;

internal class RetryPolicy : IRetryPolicy, IResettable
{
    internal RetryPolicy()
    {
    }

    public uint Attempt { get; internal set; }

    public TimeSpan LastDelay { get; internal set; }

    public bool RetryRequested { get; private set; }

    public bool Abandoned { get; private set; }

    internal void NextAttempt()
    {
        Attempt++;
        RetryRequested = false;
    }

    public void RetryAfter(TimeSpan delay)
    {
        LastDelay = delay;
        RetryRequested = true;
        Abandoned = false;
    }

    public void RetryAfter(Func<uint, TimeSpan, TimeSpan> delay)
    {
        LastDelay = delay(Attempt, LastDelay);
        RetryRequested = true;
        Abandoned = false;
    }

    public void Abandon()
    {
        Abandoned = true;
        RetryRequested = false;
    }

    public bool TryReset()
    {
        Attempt = 0;
        LastDelay = TimeSpan.Zero;
        RetryRequested = false;
        Abandoned = false;
        return true;
    }
}
