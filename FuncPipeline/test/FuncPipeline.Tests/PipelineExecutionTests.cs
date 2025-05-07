using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using Xunit;

namespace FuncPipeline.Tests;

public class PipelineExecutionTests
{
    [Fact]
    public async Task Execute_Called()
    {
        CancellationToken cancellationToken = default;

        var func = A.Fake<ITestStub>(x => x.Strict());
        A.CallTo(() => func.ExecuteAsync(cancellationToken))
            .Returns(Task.CompletedTask);

        var context = new PipelineRunContext().Set(typeof(ITestStub), func);

        IPipeline pipeline = PipelineBuilder.Create()
              .NewPipeline()
              .Execute(static async (ITestStub x, CancellationToken ct) => await x.ExecuteAsync(ct))
              .Build()
              .Pipelines[0];

        PipelineRunResult result = await pipeline.RunAsync(context, cancellationToken);

        Assert.True(result.IsSuccessful);
        Assert.Null(result.Exception);
        A.CallTo(() => func.ExecuteAsync(cancellationToken)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Calling_Next_Form_Execute_Has_No_Effect()
    {
        CancellationToken cancellationToken = default;

        var func = A.Fake<ITestStub>(x => x.Strict());
        A.CallTo(() => func.ExecuteAsync(cancellationToken))
            .Returns(Task.CompletedTask);

        var context = new PipelineRunContext().Set(typeof(ITestStub), func);

        IPipeline pipeline = PipelineBuilder.Create()
              .NewPipeline()
              .Execute(static async (ITestStub x, INext next, CancellationToken ct) =>
              {
                  await next.RunAsync();
                  await x.ExecuteAsync(ct);
              })
              .Build()
              .Pipelines[0];

        PipelineRunResult result = await pipeline.RunAsync(context, cancellationToken);

        Assert.True(result.IsSuccessful);
        Assert.Null(result.Exception);
        A.CallTo(() => func.ExecuteAsync(cancellationToken)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Execute_Called_In_Order_Of_Definition()
    {
        CancellationToken cancellationToken = default;

        var func = A.Fake<ITestStub>(x => x.Strict());
        A.CallTo(() => func.ExecuteAsync(A<string>.Ignored, cancellationToken))
            .Returns(Task.CompletedTask);

        var context = new PipelineRunContext().Set(typeof(ITestStub), func);

        IPipeline pipeline = PipelineBuilder.Create()
              .NewPipeline()
              .Execute(static async (ITestStub x, INext next, CancellationToken ct) =>
              {
                  await x.ExecuteAsync("before wrapper 1", ct);
                  await next.RunAsync();
                  await x.ExecuteAsync("after wrapper 1", ct);
              })
              .Execute(static async (ITestStub x, INext next, CancellationToken ct) =>
              {
                  await x.ExecuteAsync("before wrapper 2", ct);
                  await next.RunAsync();
                  await x.ExecuteAsync("after wrapper 2", ct);
              })
              .Execute(static async (ITestStub x, CancellationToken ct) => await x.ExecuteAsync("func", ct))
              .Build()
              .Pipelines[0];

        PipelineRunResult result = await pipeline.RunAsync(context, cancellationToken);

        Assert.True(result.IsSuccessful);
        Assert.Null(result.Exception);
        A.CallTo(() => func.ExecuteAsync("before wrapper 1", cancellationToken))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => func.ExecuteAsync("before wrapper 2", cancellationToken)).MustHaveHappenedOnceExactly())
            .Then(A.CallTo(() => func.ExecuteAsync("func", cancellationToken)).MustHaveHappenedOnceExactly())
            .Then(A.CallTo(() => func.ExecuteAsync("after wrapper 2", cancellationToken)).MustHaveHappenedOnceExactly())
            .Then(A.CallTo(() => func.ExecuteAsync("after wrapper 1", cancellationToken)).MustHaveHappenedOnceExactly());
    }

    [Fact]
    public async Task Not_Calling_Next_Short_Circuits_Pipeline()
    {
        CancellationToken cancellationToken = default;

        var func = A.Fake<ITestStub>(x => x.Strict());
        A.CallTo(() => func.ExecuteAsync(A<string>.Ignored, cancellationToken))
            .Returns(Task.CompletedTask);

        var context = new PipelineRunContext().Set(typeof(ITestStub), func);

        IPipeline pipeline = PipelineBuilder.Create()
              .NewPipeline()
              .Execute(static async (ITestStub x, INext next, CancellationToken ct) =>
              {
                  await x.ExecuteAsync("before wrapper 1", ct);
                  await next.RunAsync();
                  await x.ExecuteAsync("after wrapper 1", ct);
              })
              .Execute(static async (ITestStub x, INext next, CancellationToken ct) =>
              {
                  await x.ExecuteAsync("wrapper 2", ct);
              })
              .Execute(static async (ITestStub x, CancellationToken ct) => await x.ExecuteAsync("func", ct))
              .Build()
              .Pipelines[0];

        PipelineRunResult result = await pipeline.RunAsync(context, cancellationToken);

        Assert.True(result.IsSuccessful);
        Assert.Null(result.Exception);
        A.CallTo(() => func.ExecuteAsync("before wrapper 1", cancellationToken))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => func.ExecuteAsync("wrapper 2", cancellationToken))
           .MustHaveHappenedOnceExactly();
        A.CallTo(() => func.ExecuteAsync("after wrapper 1", cancellationToken))
           .MustHaveHappenedOnceExactly();
        A.CallTo(() => func.ExecuteAsync("func", cancellationToken))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task Context_Available_In_Result()
    {
        var context = new PipelineRunContext();

        IPipeline pipeline = PipelineBuilder.Create()
              .NewPipeline()
              .Execute(static async (ITestStub x, CancellationToken ct) => await x.ExecuteAsync(ct))
              .Build()
              .Pipelines[0];

        PipelineRunResult result = await pipeline.RunAsync(context);

        Assert.Equal(context, result.Context);
    }

    [Fact]
    public async Task Context_InternallyCreated_Available_In_Result()
    {
        IPipeline pipeline = PipelineBuilder.Create()
              .NewPipeline()
              .Execute(static async (ITestStub x, CancellationToken ct) => await x.ExecuteAsync(ct))
              .Build()
              .Pipelines[0];

        PipelineRunResult result = await pipeline.RunAsync();

        Assert.NotNull(result.Context);
    }
}
