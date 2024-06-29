namespace M.EventBrokerSlim.Internal;

internal abstract class RetryDescriptor
{
    protected RetryDescriptor(object @event, RetryPolicy retryPolicy)
    {
        Event = @event;
        RetryPolicy = retryPolicy;
    }

    public object Event { get; }

    public RetryPolicy RetryPolicy { get; }
}
