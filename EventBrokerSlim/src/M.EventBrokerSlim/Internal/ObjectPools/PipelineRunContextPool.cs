using System.Diagnostics.CodeAnalysis;
using FuncPipeline;
using Microsoft.Extensions.ObjectPool;

namespace M.EventBrokerSlim.Internal.ObjectPools;

internal static class PipelineRunContextPool
{
    [NotNull]
    internal static DefaultObjectPool<PipelineRunContext>? Instance { get; set; }
}
