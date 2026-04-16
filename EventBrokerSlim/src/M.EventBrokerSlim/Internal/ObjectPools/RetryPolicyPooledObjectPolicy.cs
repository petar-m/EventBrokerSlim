using Microsoft.Extensions.ObjectPool;

namespace M.EventBrokerSlim.Internal.ObjectPools;

internal class RetryPolicyPooledObjectPolicy : PooledObjectPolicy<RetryPolicy>
{
    public override RetryPolicy Create()
    {
        return new RetryPolicy();
    }

    public override bool Return(RetryPolicy obj)
    {
        obj.TryReset();
        return true;
    }
}
