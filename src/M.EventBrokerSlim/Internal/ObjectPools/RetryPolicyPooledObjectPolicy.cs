using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.ObjectPool;

namespace M.EventBrokerSlim.Internal.ObjectPools;

internal class RetryPolicyPooledObjectPolicy : IPooledObjectPolicy<RetryPolicy>
{
    public RetryPolicy Create()
    {
        return new RetryPolicy();
    }

    public bool Return(RetryPolicy obj)
    {
        obj.Clear();
        return true;
    }
}
