using System;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using Xunit;

namespace FuncPipeline.Tests;

public class PipelineExceptionTests
{
    [Fact]
    public async Task Pipeline_Catches_Exceptions()
    {
        CancellationToken cancellationToken = default;

        var func = A.Fake<ITestStub>(x => x.Strict());
        A.CallTo(() => func.ExecuteAsync(cancellationToken))
            .Throws(new Exception("Test"));

        var context = new PipelineRunContext().Set(typeof(ITestStub), func);

        IPipeline pipeline = PipelineBuilder.Create()
              .NewPipeline()
              .Execute(static async (ITestStub x, CancellationToken ct) =>
              {
                  await x.ExecuteAsync(ct);
              })
              .Build()
              .Pipelines[0];

        PipelineRunResult result = await pipeline.RunAsync(context, cancellationToken);

        Assert.False(result.IsSuccessful);
        Assert.IsType<Exception>(result.Exception);
        Assert.Equal("Test", result.Exception!.Message);
        A.CallTo(() => func.ExecuteAsync(cancellationToken))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Exception_Short_Circuits_Pipeline()
    {
        CancellationToken cancellationToken = default;

        var func = A.Fake<ITestStub>(x => x.Strict());
        A.CallTo(() => func.ExecuteAsync(A<string>.Ignored, cancellationToken))
            .Returns(Task.CompletedTask);

        var context = new PipelineRunContext().Set(typeof(ITestStub), func);

        IPipeline pipeline = PipelineBuilder.Create()
              .NewPipeline()
              .Execute(static async (ITestStub x, CancellationToken ct) => await x.ExecuteAsync("func", ct))
              .WrapWith(static async (ITestStub x, INext next, CancellationToken ct) =>
              {
                  await x.ExecuteAsync("wrapper 1", ct);
                  await next.RunAsync();
              })
              .WrapWith(static async (ITestStub x, INext next, CancellationToken ct) =>
              {
                  await x.ExecuteAsync("wrapper 2", ct);
                  throw new Exception("Test");
              })
              .Build()
              .Pipelines[0];

        PipelineRunResult result = await pipeline.RunAsync(context, cancellationToken);

        Assert.False(result.IsSuccessful);
        Assert.IsType<Exception>(result.Exception);
        Assert.Equal("Test", result.Exception!.Message);
        A.CallTo(() => func.ExecuteAsync("wrapper 2", cancellationToken))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => func.ExecuteAsync("wrapper 1", cancellationToken))
            .MustNotHaveHappened();
        A.CallTo(() => func.ExecuteAsync("func", cancellationToken))
            .MustNotHaveHappened();
    }
}
