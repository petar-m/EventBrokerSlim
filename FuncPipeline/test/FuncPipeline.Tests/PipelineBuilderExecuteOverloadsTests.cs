using System.Threading.Tasks;
using FakeItEasy;
using Xunit;

namespace FuncPipeline.Tests;

public class PipelineBuilderExecuteOverloadsTests
{
    [Fact]
    public async Task Inject_0_Parameters()
    {
        // Arrange
        var mock = A.Fake<IMock>(x => x.Strict());
        A.CallTo(() => mock.Do())
         .Returns(Task.CompletedTask);

        var context = new PipelineRunContext()
            .Set<IMock>(mock);

        IPipeline pipeline = PipelineBuilder.Create()
              .NewPipeline()
              .Execute(mock.Do)
              .Build()
              .Pipelines[0];

        // Act
        PipelineRunResult result = await pipeline.RunAsync(context);

        // Assert
        Assert.True(result.IsSuccessful, result.Exception?.ToString());
        Assert.Null(result.Exception);
        A.CallTo(() => mock.Do())
         .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Inject_1_Parameters()
    {
        // Arrange
        var mock = A.Fake<IMock>(x => x.Strict());
        A.CallTo(() => mock.Do())
         .Returns(Task.CompletedTask);

        var context = new PipelineRunContext()
            .Set<IMock>(mock);

        IPipeline pipeline = PipelineBuilder.Create()
              .NewPipeline()
              .Execute(static async (IMock x) => await x.Do())
              .Build()
              .Pipelines[0];

        // Act
        PipelineRunResult result = await pipeline.RunAsync(context);

        // Assert
        Assert.True(result.IsSuccessful, result.Exception?.ToString());
        Assert.Null(result.Exception);
        A.CallTo(() => mock.Do())
         .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Inject_2_Parameters()
    {
        // Arrange
        var arg1 = A.Fake<IArg1>(x => x.Strict());

        var mock = A.Fake<IMock>(x => x.Strict());
        A.CallTo(() => mock.Do(arg1))
         .Returns(Task.CompletedTask);

        var context = new PipelineRunContext()
            .Set<IMock>(mock)
            .Set<IArg1>(arg1);

        IPipeline pipeline = PipelineBuilder.Create()
              .NewPipeline()
              .Execute(static async (IMock x, IArg1 arg1) => await x.Do(arg1))
              .Build()
              .Pipelines[0];

        // Act
        PipelineRunResult result = await pipeline.RunAsync(context);

        // Assert
        Assert.True(result.IsSuccessful, result.Exception?.ToString());
        Assert.Null(result.Exception);
        A.CallTo(() => mock.Do(arg1))
         .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Inject_3_Parameters()
    {
        // Arrange
        var arg1 = A.Fake<IArg1>(x => x.Strict());
        var arg2 = A.Fake<IArg2>(x => x.Strict());

        var mock = A.Fake<IMock>(x => x.Strict());
        A.CallTo(() => mock.Do(arg1, arg2))
         .Returns(Task.CompletedTask);

        var context = new PipelineRunContext()
            .Set<IMock>(mock)
            .Set<IArg1>(arg1)
            .Set<IArg2>(arg2);

        IPipeline pipeline = PipelineBuilder.Create()
              .NewPipeline()
              .Execute(static async (IMock x, IArg1 arg1, IArg2 arg2) => await x.Do(arg1, arg2))
              .Build()
              .Pipelines[0];

        // Act
        PipelineRunResult result = await pipeline.RunAsync(context);

        // Assert
        Assert.True(result.IsSuccessful, result.Exception?.ToString());
        Assert.Null(result.Exception);
        A.CallTo(() => mock.Do(arg1, arg2))
         .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Inject_4_Parameters()
    {
        // Arrange
        var arg1 = A.Fake<IArg1>(x => x.Strict());
        var arg2 = A.Fake<IArg2>(x => x.Strict());
        var arg3 = A.Fake<IArg3>(x => x.Strict());

        var mock = A.Fake<IMock>(x => x.Strict());
        A.CallTo(() => mock.Do(arg1, arg2, arg3))
         .Returns(Task.CompletedTask);

        var context = new PipelineRunContext()
            .Set<IMock>(mock)
            .Set<IArg1>(arg1)
            .Set<IArg2>(arg2)
            .Set<IArg3>(arg3);

        IPipeline pipeline = PipelineBuilder.Create()
              .NewPipeline()
              .Execute(static async (IMock x, IArg1 arg1, IArg2 arg2, IArg3 arg3) => await x.Do(arg1, arg2, arg3))
              .Build()
              .Pipelines[0];

        // Act
        PipelineRunResult result = await pipeline.RunAsync(context);

        // Assert
        Assert.True(result.IsSuccessful, result.Exception?.ToString());
        Assert.Null(result.Exception);
        A.CallTo(() => mock.Do(arg1, arg2, arg3))
         .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Inject_5_Parameters()
    {
        // Arrange
        var arg1 = A.Fake<IArg1>(x => x.Strict());
        var arg2 = A.Fake<IArg2>(x => x.Strict());
        var arg3 = A.Fake<IArg3>(x => x.Strict());
        var arg4 = A.Fake<IArg4>(x => x.Strict());

        var mock = A.Fake<IMock>(x => x.Strict());
        A.CallTo(() => mock.Do(arg1, arg2, arg3, arg4))
         .Returns(Task.CompletedTask);

        var context = new PipelineRunContext()
            .Set<IMock>(mock)
            .Set<IArg1>(arg1)
            .Set<IArg2>(arg2)
            .Set<IArg3>(arg3)
            .Set<IArg4>(arg4);

        IPipeline pipeline = PipelineBuilder.Create()
              .NewPipeline()
              .Execute(static async (IMock x, IArg1 arg1, IArg2 arg2, IArg3 arg3, IArg4 arg4) => await x.Do(arg1, arg2, arg3, arg4))
              .Build()
              .Pipelines[0];

        // Act
        PipelineRunResult result = await pipeline.RunAsync(context);

        // Assert
        Assert.True(result.IsSuccessful, result.Exception?.ToString());
        Assert.Null(result.Exception);
        A.CallTo(() => mock.Do(arg1, arg2, arg3, arg4))
         .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Inject_6_Parameters()
    {
        // Arrange
        var arg1 = A.Fake<IArg1>(x => x.Strict());
        var arg2 = A.Fake<IArg2>(x => x.Strict());
        var arg3 = A.Fake<IArg3>(x => x.Strict());
        var arg4 = A.Fake<IArg4>(x => x.Strict());
        var arg5 = A.Fake<IArg5>(x => x.Strict());

        var mock = A.Fake<IMock>(x => x.Strict());
        A.CallTo(() => mock.Do(arg1, arg2, arg3, arg4, arg5))
         .Returns(Task.CompletedTask);

        var context = new PipelineRunContext()
            .Set<IMock>(mock)
            .Set<IArg1>(arg1)
            .Set<IArg2>(arg2)
            .Set<IArg3>(arg3)
            .Set<IArg4>(arg4)
            .Set<IArg5>(arg5);

        IPipeline pipeline = PipelineBuilder.Create()
              .NewPipeline()
              .Execute(static async (IMock x, IArg1 arg1, IArg2 arg2, IArg3 arg3, IArg4 arg4, IArg5 arg5) => await x.Do(arg1, arg2, arg3, arg4, arg5))
              .Build()
              .Pipelines[0];

        // Act
        PipelineRunResult result = await pipeline.RunAsync(context);

        // Assert
        Assert.True(result.IsSuccessful, result.Exception?.ToString());
        Assert.Null(result.Exception);
        A.CallTo(() => mock.Do(arg1, arg2, arg3, arg4, arg5))
         .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Inject_7_Parameters()
    {
        // Arrange
        var arg1 = A.Fake<IArg1>(x => x.Strict());
        var arg2 = A.Fake<IArg2>(x => x.Strict());
        var arg3 = A.Fake<IArg3>(x => x.Strict());
        var arg4 = A.Fake<IArg4>(x => x.Strict());
        var arg5 = A.Fake<IArg5>(x => x.Strict());
        var arg6 = A.Fake<IArg6>(x => x.Strict());

        var mock = A.Fake<IMock>(x => x.Strict());
        A.CallTo(() => mock.Do(arg1, arg2, arg3, arg4, arg5, arg6))
         .Returns(Task.CompletedTask);

        var context = new PipelineRunContext()
            .Set<IMock>(mock)
            .Set<IArg1>(arg1)
            .Set<IArg2>(arg2)
            .Set<IArg3>(arg3)
            .Set<IArg4>(arg4)
            .Set<IArg5>(arg5)
            .Set<IArg6>(arg6);

        IPipeline pipeline = PipelineBuilder.Create()
              .NewPipeline()
              .Execute(static async (IMock x, IArg1 arg1, IArg2 arg2, IArg3 arg3, IArg4 arg4, IArg5 arg5, IArg6 arg6) => await x.Do(arg1, arg2, arg3, arg4, arg5, arg6))
              .Build()
              .Pipelines[0];

        // Act
        PipelineRunResult result = await pipeline.RunAsync(context);

        // Assert
        Assert.True(result.IsSuccessful, result.Exception?.ToString());
        Assert.Null(result.Exception);
        A.CallTo(() => mock.Do(arg1, arg2, arg3, arg4, arg5, arg6))
         .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Inject_8_Parameters()
    {
        // Arrange
        var arg1 = A.Fake<IArg1>(x => x.Strict());
        var arg2 = A.Fake<IArg2>(x => x.Strict());
        var arg3 = A.Fake<IArg3>(x => x.Strict());
        var arg4 = A.Fake<IArg4>(x => x.Strict());
        var arg5 = A.Fake<IArg5>(x => x.Strict());
        var arg6 = A.Fake<IArg6>(x => x.Strict());
        var arg7 = A.Fake<IArg7>(x => x.Strict());

        var mock = A.Fake<IMock>(x => x.Strict());
        A.CallTo(() => mock.Do(arg1, arg2, arg3, arg4, arg5, arg6, arg7))
         .Returns(Task.CompletedTask);

        var context = new PipelineRunContext()
            .Set<IMock>(mock)
            .Set<IArg1>(arg1)
            .Set<IArg2>(arg2)
            .Set<IArg3>(arg3)
            .Set<IArg4>(arg4)
            .Set<IArg5>(arg5)
            .Set<IArg6>(arg6)
            .Set<IArg7>(arg7);

        IPipeline pipeline = PipelineBuilder.Create()
              .NewPipeline()
              .Execute(static async (IMock x, IArg1 arg1, IArg2 arg2, IArg3 arg3, IArg4 arg4, IArg5 arg5, IArg6 arg6, IArg7 arg7) => await x.Do(arg1, arg2, arg3, arg4, arg5, arg6, arg7))
              .Build()
              .Pipelines[0];

        // Act
        PipelineRunResult result = await pipeline.RunAsync(context);

        // Assert
        Assert.True(result.IsSuccessful, result.Exception?.ToString());
        Assert.Null(result.Exception);
        A.CallTo(() => mock.Do(arg1, arg2, arg3, arg4, arg5, arg6, arg7))
         .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Inject_9_Parameters()
    {
        // Arrange
        var arg1 = A.Fake<IArg1>(x => x.Strict());
        var arg2 = A.Fake<IArg2>(x => x.Strict());
        var arg3 = A.Fake<IArg3>(x => x.Strict());
        var arg4 = A.Fake<IArg4>(x => x.Strict());
        var arg5 = A.Fake<IArg5>(x => x.Strict());
        var arg6 = A.Fake<IArg6>(x => x.Strict());
        var arg7 = A.Fake<IArg7>(x => x.Strict());
        var arg8 = A.Fake<IArg8>(x => x.Strict());

        var mock = A.Fake<IMock>(x => x.Strict());
        A.CallTo(() => mock.Do(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8))
         .Returns(Task.CompletedTask);

        var context = new PipelineRunContext()
            .Set<IMock>(mock)
            .Set<IArg1>(arg1)
            .Set<IArg2>(arg2)
            .Set<IArg3>(arg3)
            .Set<IArg4>(arg4)
            .Set<IArg5>(arg5)
            .Set<IArg6>(arg6)
            .Set<IArg7>(arg7)
            .Set<IArg8>(arg8);

        IPipeline pipeline = PipelineBuilder.Create()
              .NewPipeline()
              .Execute(static async (IMock x, IArg1 arg1, IArg2 arg2, IArg3 arg3, IArg4 arg4, IArg5 arg5, IArg6 arg6, IArg7 arg7, IArg8 arg8) => await x.Do(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8))
              .Build()
              .Pipelines[0];

        // Act
        PipelineRunResult result = await pipeline.RunAsync(context);

        // Assert
        Assert.True(result.IsSuccessful, result.Exception?.ToString());
        Assert.Null(result.Exception);
        A.CallTo(() => mock.Do(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8))
         .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Inject_10_Parameters()
    {
        // Arrange
        var arg1 = A.Fake<IArg1>(x => x.Strict());
        var arg2 = A.Fake<IArg2>(x => x.Strict());
        var arg3 = A.Fake<IArg3>(x => x.Strict());
        var arg4 = A.Fake<IArg4>(x => x.Strict());
        var arg5 = A.Fake<IArg5>(x => x.Strict());
        var arg6 = A.Fake<IArg6>(x => x.Strict());
        var arg7 = A.Fake<IArg7>(x => x.Strict());
        var arg8 = A.Fake<IArg8>(x => x.Strict());
        var arg9 = A.Fake<IArg9>(x => x.Strict());

        var mock = A.Fake<IMock>(x => x.Strict());
        A.CallTo(() => mock.Do(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9))
         .Returns(Task.CompletedTask);

        var context = new PipelineRunContext()
            .Set<IMock>(mock)
            .Set<IArg1>(arg1)
            .Set<IArg2>(arg2)
            .Set<IArg3>(arg3)
            .Set<IArg4>(arg4)
            .Set<IArg5>(arg5)
            .Set<IArg6>(arg6)
            .Set<IArg7>(arg7)
            .Set<IArg8>(arg8)
            .Set<IArg9>(arg9);

        IPipeline pipeline = PipelineBuilder.Create()
              .NewPipeline()
              .Execute(static async (IMock x, IArg1 arg1, IArg2 arg2, IArg3 arg3, IArg4 arg4, IArg5 arg5, IArg6 arg6, IArg7 arg7, IArg8 arg8, IArg9 arg9) => await x.Do(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9))
              .Build()
              .Pipelines[0];

        // Act
        PipelineRunResult result = await pipeline.RunAsync(context);

        // Assert
        Assert.True(result.IsSuccessful, result.Exception?.ToString());
        Assert.Null(result.Exception);
        A.CallTo(() => mock.Do(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9))
         .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Inject_11_Parameters()
    {
        // Arrange
        var arg1 = A.Fake<IArg1>(x => x.Strict());
        var arg2 = A.Fake<IArg2>(x => x.Strict());
        var arg3 = A.Fake<IArg3>(x => x.Strict());
        var arg4 = A.Fake<IArg4>(x => x.Strict());
        var arg5 = A.Fake<IArg5>(x => x.Strict());
        var arg6 = A.Fake<IArg6>(x => x.Strict());
        var arg7 = A.Fake<IArg7>(x => x.Strict());
        var arg8 = A.Fake<IArg8>(x => x.Strict());
        var arg9 = A.Fake<IArg9>(x => x.Strict());
        var arg10 = A.Fake<IArg10>(x => x.Strict());

        var mock = A.Fake<IMock>(x => x.Strict());
        A.CallTo(() => mock.Do(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10))
         .Returns(Task.CompletedTask);

        var context = new PipelineRunContext()
            .Set<IMock>(mock)
            .Set<IArg1>(arg1)
            .Set<IArg2>(arg2)
            .Set<IArg3>(arg3)
            .Set<IArg4>(arg4)
            .Set<IArg5>(arg5)
            .Set<IArg6>(arg6)
            .Set<IArg7>(arg7)
            .Set<IArg8>(arg8)
            .Set<IArg9>(arg9)
            .Set<IArg10>(arg10);

        IPipeline pipeline = PipelineBuilder.Create()
              .NewPipeline()
              .Execute(static async (IMock x, IArg1 arg1, IArg2 arg2, IArg3 arg3, IArg4 arg4, IArg5 arg5, IArg6 arg6, IArg7 arg7, IArg8 arg8, IArg9 arg9, IArg10 arg10) => await x.Do(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10))
              .Build()
              .Pipelines[0];

        // Act
        PipelineRunResult result = await pipeline.RunAsync(context);

        // Assert
        Assert.True(result.IsSuccessful, result.Exception?.ToString());
        Assert.Null(result.Exception);
        A.CallTo(() => mock.Do(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10))
         .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Inject_12_Parameters()
    {
        // Arrange
        var arg1 = A.Fake<IArg1>(x => x.Strict());
        var arg2 = A.Fake<IArg2>(x => x.Strict());
        var arg3 = A.Fake<IArg3>(x => x.Strict());
        var arg4 = A.Fake<IArg4>(x => x.Strict());
        var arg5 = A.Fake<IArg5>(x => x.Strict());
        var arg6 = A.Fake<IArg6>(x => x.Strict());
        var arg7 = A.Fake<IArg7>(x => x.Strict());
        var arg8 = A.Fake<IArg8>(x => x.Strict());
        var arg9 = A.Fake<IArg9>(x => x.Strict());
        var arg10 = A.Fake<IArg10>(x => x.Strict());
        var arg11 = A.Fake<IArg11>(x => x.Strict());

        var mock = A.Fake<IMock>(x => x.Strict());
        A.CallTo(() => mock.Do(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11))
         .Returns(Task.CompletedTask);

        var context = new PipelineRunContext()
            .Set<IMock>(mock)
            .Set<IArg1>(arg1)
            .Set<IArg2>(arg2)
            .Set<IArg3>(arg3)
            .Set<IArg4>(arg4)
            .Set<IArg5>(arg5)
            .Set<IArg6>(arg6)
            .Set<IArg7>(arg7)
            .Set<IArg8>(arg8)
            .Set<IArg9>(arg9)
            .Set<IArg10>(arg10)
            .Set<IArg11>(arg11);

        IPipeline pipeline = PipelineBuilder.Create()
              .NewPipeline()
              .Execute(static async (IMock x, IArg1 arg1, IArg2 arg2, IArg3 arg3, IArg4 arg4, IArg5 arg5, IArg6 arg6, IArg7 arg7, IArg8 arg8, IArg9 arg9, IArg10 arg10, IArg11 arg11) => await x.Do(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11))
              .Build()
              .Pipelines[0];

        // Act
        PipelineRunResult result = await pipeline.RunAsync(context);

        // Assert
        Assert.True(result.IsSuccessful, result.Exception?.ToString());
        Assert.Null(result.Exception);
        A.CallTo(() => mock.Do(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11))
         .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Inject_13_Parameters()
    {
        // Arrange
        var arg1 = A.Fake<IArg1>(x => x.Strict());
        var arg2 = A.Fake<IArg2>(x => x.Strict());
        var arg3 = A.Fake<IArg3>(x => x.Strict());
        var arg4 = A.Fake<IArg4>(x => x.Strict());
        var arg5 = A.Fake<IArg5>(x => x.Strict());
        var arg6 = A.Fake<IArg6>(x => x.Strict());
        var arg7 = A.Fake<IArg7>(x => x.Strict());
        var arg8 = A.Fake<IArg8>(x => x.Strict());
        var arg9 = A.Fake<IArg9>(x => x.Strict());
        var arg10 = A.Fake<IArg10>(x => x.Strict());
        var arg11 = A.Fake<IArg11>(x => x.Strict());
        var arg12 = A.Fake<IArg12>(x => x.Strict());

        var mock = A.Fake<IMock>(x => x.Strict());
        A.CallTo(() => mock.Do(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12))
         .Returns(Task.CompletedTask);

        var context = new PipelineRunContext()
            .Set<IMock>(mock)
            .Set<IArg1>(arg1)
            .Set<IArg2>(arg2)
            .Set<IArg3>(arg3)
            .Set<IArg4>(arg4)
            .Set<IArg5>(arg5)
            .Set<IArg6>(arg6)
            .Set<IArg7>(arg7)
            .Set<IArg8>(arg8)
            .Set<IArg9>(arg9)
            .Set<IArg10>(arg10)
            .Set<IArg11>(arg11)
            .Set<IArg12>(arg12);

        IPipeline pipeline = PipelineBuilder.Create()
              .NewPipeline()
              .Execute(static async (IMock x, IArg1 arg1, IArg2 arg2, IArg3 arg3, IArg4 arg4, IArg5 arg5, IArg6 arg6, IArg7 arg7, IArg8 arg8, IArg9 arg9, IArg10 arg10, IArg11 arg11, IArg12 arg12) => await x.Do(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12))
              .Build()
              .Pipelines[0];

        // Act
        PipelineRunResult result = await pipeline.RunAsync(context);

        // Assert
        Assert.True(result.IsSuccessful, result.Exception?.ToString());
        Assert.Null(result.Exception);
        A.CallTo(() => mock.Do(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12))
         .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Inject_14_Parameters()
    {
        // Arrange
        var arg1 = A.Fake<IArg1>(x => x.Strict());
        var arg2 = A.Fake<IArg2>(x => x.Strict());
        var arg3 = A.Fake<IArg3>(x => x.Strict());
        var arg4 = A.Fake<IArg4>(x => x.Strict());
        var arg5 = A.Fake<IArg5>(x => x.Strict());
        var arg6 = A.Fake<IArg6>(x => x.Strict());
        var arg7 = A.Fake<IArg7>(x => x.Strict());
        var arg8 = A.Fake<IArg8>(x => x.Strict());
        var arg9 = A.Fake<IArg9>(x => x.Strict());
        var arg10 = A.Fake<IArg10>(x => x.Strict());
        var arg11 = A.Fake<IArg11>(x => x.Strict());
        var arg12 = A.Fake<IArg12>(x => x.Strict());
        var arg13 = A.Fake<IArg13>(x => x.Strict());

        var mock = A.Fake<IMock>(x => x.Strict());
        A.CallTo(() => mock.Do(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13))
         .Returns(Task.CompletedTask);

        var context = new PipelineRunContext()
            .Set<IMock>(mock)
            .Set<IArg1>(arg1)
            .Set<IArg2>(arg2)
            .Set<IArg3>(arg3)
            .Set<IArg4>(arg4)
            .Set<IArg5>(arg5)
            .Set<IArg6>(arg6)
            .Set<IArg7>(arg7)
            .Set<IArg8>(arg8)
            .Set<IArg9>(arg9)
            .Set<IArg10>(arg10)
            .Set<IArg11>(arg11)
            .Set<IArg12>(arg12)
            .Set<IArg13>(arg13);

        IPipeline pipeline = PipelineBuilder.Create()
              .NewPipeline()
              .Execute(static async (IMock x, IArg1 arg1, IArg2 arg2, IArg3 arg3, IArg4 arg4, IArg5 arg5, IArg6 arg6, IArg7 arg7, IArg8 arg8, IArg9 arg9, IArg10 arg10, IArg11 arg11, IArg12 arg12, IArg13 arg13) => await x.Do(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13))
              .Build()
              .Pipelines[0];

        // Act
        PipelineRunResult result = await pipeline.RunAsync(context);

        // Assert
        Assert.True(result.IsSuccessful, result.Exception?.ToString());
        Assert.Null(result.Exception);
        A.CallTo(() => mock.Do(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13))
         .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Inject_15_Parameters()
    {
        // Arrange
        var arg1 = A.Fake<IArg1>(x => x.Strict());
        var arg2 = A.Fake<IArg2>(x => x.Strict());
        var arg3 = A.Fake<IArg3>(x => x.Strict());
        var arg4 = A.Fake<IArg4>(x => x.Strict());
        var arg5 = A.Fake<IArg5>(x => x.Strict());
        var arg6 = A.Fake<IArg6>(x => x.Strict());
        var arg7 = A.Fake<IArg7>(x => x.Strict());
        var arg8 = A.Fake<IArg8>(x => x.Strict());
        var arg9 = A.Fake<IArg9>(x => x.Strict());
        var arg10 = A.Fake<IArg10>(x => x.Strict());
        var arg11 = A.Fake<IArg11>(x => x.Strict());
        var arg12 = A.Fake<IArg12>(x => x.Strict());
        var arg13 = A.Fake<IArg13>(x => x.Strict());
        var arg14 = A.Fake<IArg14>(x => x.Strict());

        var mock = A.Fake<IMock>(x => x.Strict());
        A.CallTo(() => mock.Do(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14))
         .Returns(Task.CompletedTask);

        var context = new PipelineRunContext()
            .Set<IMock>(mock)
            .Set<IArg1>(arg1)
            .Set<IArg2>(arg2)
            .Set<IArg3>(arg3)
            .Set<IArg4>(arg4)
            .Set<IArg5>(arg5)
            .Set<IArg6>(arg6)
            .Set<IArg7>(arg7)
            .Set<IArg8>(arg8)
            .Set<IArg9>(arg9)
            .Set<IArg10>(arg10)
            .Set<IArg11>(arg11)
            .Set<IArg12>(arg12)
            .Set<IArg13>(arg13)
            .Set<IArg14>(arg14);

        IPipeline pipeline = PipelineBuilder.Create()
              .NewPipeline()
              .Execute(static async (IMock x, IArg1 arg1, IArg2 arg2, IArg3 arg3, IArg4 arg4, IArg5 arg5, IArg6 arg6, IArg7 arg7, IArg8 arg8, IArg9 arg9, IArg10 arg10, IArg11 arg11, IArg12 arg12, IArg13 arg13, IArg14 arg14) => await x.Do(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14))
              .Build()
              .Pipelines[0];

        // Act
        PipelineRunResult result = await pipeline.RunAsync(context);

        // Assert
        Assert.True(result.IsSuccessful, result.Exception?.ToString());
        Assert.Null(result.Exception);
        A.CallTo(() => mock.Do(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14))
         .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Inject_16_Parameters()
    {
        // Arrange
        var arg1 = A.Fake<IArg1>(x => x.Strict());
        var arg2 = A.Fake<IArg2>(x => x.Strict());
        var arg3 = A.Fake<IArg3>(x => x.Strict());
        var arg4 = A.Fake<IArg4>(x => x.Strict());
        var arg5 = A.Fake<IArg5>(x => x.Strict());
        var arg6 = A.Fake<IArg6>(x => x.Strict());
        var arg7 = A.Fake<IArg7>(x => x.Strict());
        var arg8 = A.Fake<IArg8>(x => x.Strict());
        var arg9 = A.Fake<IArg9>(x => x.Strict());
        var arg10 = A.Fake<IArg10>(x => x.Strict());
        var arg11 = A.Fake<IArg11>(x => x.Strict());
        var arg12 = A.Fake<IArg12>(x => x.Strict());
        var arg13 = A.Fake<IArg13>(x => x.Strict());
        var arg14 = A.Fake<IArg14>(x => x.Strict());
        var arg15 = A.Fake<IArg15>(x => x.Strict());

        var mock = A.Fake<IMock>(x => x.Strict());
        A.CallTo(() => mock.Do(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15))
         .Returns(Task.CompletedTask);

        var context = new PipelineRunContext()
            .Set<IMock>(mock)
            .Set<IArg1>(arg1)
            .Set<IArg2>(arg2)
            .Set<IArg3>(arg3)
            .Set<IArg4>(arg4)
            .Set<IArg5>(arg5)
            .Set<IArg6>(arg6)
            .Set<IArg7>(arg7)
            .Set<IArg8>(arg8)
            .Set<IArg9>(arg9)
            .Set<IArg10>(arg10)
            .Set<IArg11>(arg11)
            .Set<IArg12>(arg12)
            .Set<IArg13>(arg13)
            .Set<IArg14>(arg14)
            .Set<IArg15>(arg15);

        IPipeline pipeline = PipelineBuilder.Create()
              .NewPipeline()
              .Execute(static async (IMock x, IArg1 arg1, IArg2 arg2, IArg3 arg3, IArg4 arg4, IArg5 arg5, IArg6 arg6, IArg7 arg7, IArg8 arg8, IArg9 arg9, IArg10 arg10, IArg11 arg11, IArg12 arg12, IArg13 arg13, IArg14 arg14, IArg15 arg15) => await x.Do(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15))
              .Build()
              .Pipelines[0];

        // Act
        PipelineRunResult result = await pipeline.RunAsync(context);

        // Assert
        Assert.True(result.IsSuccessful, result.Exception?.ToString());
        Assert.Null(result.Exception);
        A.CallTo(() => mock.Do(arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15))
         .MustHaveHappenedOnceExactly();
    }
}
