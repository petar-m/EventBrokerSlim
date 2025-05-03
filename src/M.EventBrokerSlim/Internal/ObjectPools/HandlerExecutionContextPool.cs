using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.ObjectPool;

namespace M.EventBrokerSlim.Internal.ObjectPools;

internal static class HandlerExecutionContextPool
{
    [NotNull]
    internal static DefaultObjectPool<HandlerExecutionContext>? Instance { get; set; }
}
