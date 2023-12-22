namespace M.EventBrokerSlim.Tests;

public interface ITraceable<T>
{
    public T CorrelationId { get; }
}
