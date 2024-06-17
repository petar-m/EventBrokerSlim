namespace M.EventBrokerSlim.Internal;

internal sealed class EventHandlerRetryDescriptor : RetryDescriptor
{
    public EventHandlerRetryDescriptor(object @event, EventHandlerDescriptor eventHandlerDescriptor, RetryPolicy retryPolicy) :
        base(@event, retryPolicy)
    {
        EventHandlerDescriptor = eventHandlerDescriptor;
    }

    public EventHandlerDescriptor EventHandlerDescriptor { get; }
}
