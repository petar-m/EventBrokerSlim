namespace M.EventBrokerSlim.Internal;

internal record RetryDescriptor(object Event, EventHandlerDescriptor EventHandlerDescriptor, RetryPolicy RetryPolicy);
