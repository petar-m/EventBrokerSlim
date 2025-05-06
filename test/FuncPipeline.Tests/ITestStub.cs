using System.Threading;
using System.Threading.Tasks;

namespace FuncPipeline.Tests;

public interface ITestStub
{
    Task ExecuteAsync(CancellationToken cancellationToken);

    Task ExecuteAsync<T>(T? value, CancellationToken cancellationToken);

    T Execute<T>();
}
