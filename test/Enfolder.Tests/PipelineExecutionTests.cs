
using FakeItEasy;

namespace Enfolder.Tests;

public class PipelineExecutionTests
{
    [Fact]
    public async Task Execute_Called()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var func = A.Fake<ITestStub>(x => x.Strict());
        A.CallTo(() => func.ExecuteAsync(cancellationToken))
            .Returns(Task.CompletedTask);

        var context = new PipelineRunContext().Set(func);

        IPipeline pipeline = new PipelineBuilder()
              .For("1")
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
        var cancellationToken = TestContext.Current.CancellationToken;

        var func = A.Fake<ITestStub>(x => x.Strict());
        A.CallTo(() => func.ExecuteAsync(cancellationToken))
            .Returns(Task.CompletedTask);

        var context = new PipelineRunContext().Set(func);

        IPipeline pipeline = new PipelineBuilder()
              .For("1")
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
    public async Task WrapWith_Called_In_Order_Before_Execute()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var func = A.Fake<ITestStub>(x => x.Strict());
        A.CallTo(() => func.ExecuteAsync(A<string>.Ignored, cancellationToken))
            .Returns(Task.CompletedTask);

        var context = new PipelineRunContext().Set(func);

        IPipeline pipeline = new PipelineBuilder()
              .For("1")
              .Execute(static async (ITestStub x, CancellationToken ct) => await x.ExecuteAsync("func", ct))
              .WrapWith(static async (ITestStub x, INext next, CancellationToken ct) =>
              {
                  await x.ExecuteAsync("wrapper 1", ct);
                  await next.RunAsync();
              })
              .WrapWith(static async (ITestStub x, INext next, CancellationToken ct) =>
              {
                  await x.ExecuteAsync("wrapper 2", ct);
                  await next.RunAsync();
              })
              .Build()
              .Pipelines[0];

        PipelineRunResult result = await pipeline.RunAsync(context, cancellationToken);

        Assert.True(result.IsSuccessful);
        Assert.Null(result.Exception);
        A.CallTo(() => func.ExecuteAsync("wrapper 2", cancellationToken))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => func.ExecuteAsync("wrapper 1", cancellationToken)).MustHaveHappenedOnceExactly())
            .Then(A.CallTo(() => func.ExecuteAsync("func", cancellationToken)).MustHaveHappenedOnceExactly());
    }

    [Fact]
    public async Task Not_Calling_Next_Short_Circuits_Pipeline()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var func = A.Fake<ITestStub>(x => x.Strict());
        A.CallTo(() => func.ExecuteAsync(A<string>.Ignored, cancellationToken))
            .Returns(Task.CompletedTask);

        var context = new PipelineRunContext().Set(func);

        IPipeline pipeline = new PipelineBuilder()
              .For("1")
              .Execute(static async (ITestStub x, CancellationToken ct) => await x.ExecuteAsync("func", ct))
              .WrapWith(static async (ITestStub x, INext next, CancellationToken ct) =>
              {
                  await x.ExecuteAsync("wrapper 1", ct);
                  await next.RunAsync();
              })
              .WrapWith(static async (ITestStub x, INext next, CancellationToken ct) =>
              {
                  await x.ExecuteAsync("wrapper 2", ct);
              })
              .Build()
              .Pipelines[0];

        PipelineRunResult result = await pipeline.RunAsync(context, cancellationToken);

        Assert.True(result.IsSuccessful);
        Assert.Null(result.Exception);
        A.CallTo(() => func.ExecuteAsync("wrapper 2", cancellationToken))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => func.ExecuteAsync("wrapper 1", cancellationToken))
           .MustNotHaveHappened();
        A.CallTo(() => func.ExecuteAsync("func", cancellationToken))
            .MustNotHaveHappened();
    }
}
