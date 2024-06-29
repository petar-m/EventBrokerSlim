namespace M.EventBrokerSlim.Internal;

internal sealed class DelegateHandlerRetryDescriptor : RetryDescriptor
{
    public DelegateHandlerRetryDescriptor(object @event, DelegateHandlerDescriptor delegateHandlerDescriptor, RetryPolicy retryPolicy) :
        base(@event, retryPolicy)
    {
        DelegateHandlerDescriptor = delegateHandlerDescriptor;
    }

    public DelegateHandlerDescriptor DelegateHandlerDescriptor { get; }
}
