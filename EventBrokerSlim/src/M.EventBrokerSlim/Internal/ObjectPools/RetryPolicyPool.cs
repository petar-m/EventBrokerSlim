using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.ObjectPool;

namespace M.EventBrokerSlim.Internal.ObjectPools;

internal static class RetryPolicyPool
{
    [NotNull]
    internal static DefaultObjectPool<RetryPolicy>? Instance { get; set; }
}
