namespace M.EventBrokerSlim.Tests;

public interface IIdentifieableEvent<T>
{
    public T Id { get; }
}