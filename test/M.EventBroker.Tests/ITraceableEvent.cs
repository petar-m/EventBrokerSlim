namespace M.EventBrokerSlim.Tests;

public interface ITraceableEvent<T>
{
    public T CorrelationId { get; }
}
