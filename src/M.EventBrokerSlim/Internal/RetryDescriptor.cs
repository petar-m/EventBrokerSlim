using Enfolder;

namespace M.EventBrokerSlim.Internal;

internal class RetryDescriptor
{
    internal RetryDescriptor(object @event, RetryPolicy retryPolicy, IPipeline pipeline)
    {
        Event = @event;
        RetryPolicy = retryPolicy;
        Pipeline = pipeline;
    }

    public object Event { get; }

    public RetryPolicy RetryPolicy { get; }

    public IPipeline Pipeline { get; }
}
