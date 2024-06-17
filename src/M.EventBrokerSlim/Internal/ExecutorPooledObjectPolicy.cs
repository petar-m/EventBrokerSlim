using Microsoft.Extensions.ObjectPool;

namespace M.EventBrokerSlim.Internal;

internal sealed class ExecutorPooledObjectPolicy : IPooledObjectPolicy<ThreadPoolEventHandlerRunner.Executor>
{
    public ThreadPoolEventHandlerRunner.Executor Create()
    {
        return new ThreadPoolEventHandlerRunner.Executor();
    }

    public bool Return(ThreadPoolEventHandlerRunner.Executor obj)
    {
        obj.Clear();
        return true;
    }
}
