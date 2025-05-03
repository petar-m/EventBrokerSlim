using Enfolder;
using Microsoft.Extensions.ObjectPool;

namespace M.EventBrokerSlim.Internal.ObjectPools;

internal class PipelineRunContextPooledObjectPolicy : IPooledObjectPolicy<PipelineRunContext>
{
    public PipelineRunContext Create()
    {
        return new PipelineRunContext();
    }

    public bool Return(PipelineRunContext obj)
    {
        obj.Clear();
        return true;
    }
}
