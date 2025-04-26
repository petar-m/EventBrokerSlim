namespace Enfolder.Tests;

public interface ITestStub
{
    Task ExecuteAsync(CancellationToken cancellationToken);

    Task ExecuteAsync(string value, CancellationToken cancellationToken);
}
