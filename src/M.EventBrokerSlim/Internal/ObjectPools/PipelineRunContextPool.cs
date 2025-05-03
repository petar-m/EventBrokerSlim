using System.Diagnostics.CodeAnalysis;
using Enfolder;
using Microsoft.Extensions.ObjectPool;

namespace M.EventBrokerSlim.Internal.ObjectPools;

internal static class PipelineRunContextPool
{
    [NotNull]
    internal static DefaultObjectPool<PipelineRunContext>? Instance { get; set; }
}
