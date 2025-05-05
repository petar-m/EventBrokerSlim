using System;
using System.Threading.Tasks;
using Xunit;

namespace Enfolder.Tests;

public class ArgumentsResolutionUnknownPropertyValuesTests
{
    [Fact]
    public async Task Source_Unknown_Throws()
    {
        // Arrange
        IPipeline pipeline = PipelineBuilder.Create()
            .NewPipeline()
            .Execute(static ([ResolveFrom(PrimarySource = (Source)int.MaxValue, Fallback = false, PrimaryNotFound = NotFoundBehavior.ReturnTypeDefault)] ITestStub x) =>
            {
                Assert.Null(x);
                return Task.CompletedTask;
            })
            .Build()
            .Pipelines[0];

        // Act
        PipelineRunResult result = await pipeline.RunAsync();

        // Assert
        Assert.False(result.IsSuccessful);

        Assert.IsType<ArgumentException>(result.Exception);
        Assert.Equal("Source enum value 2147483647 is not supported. ResolveFromAttribute { PrimarySource = 2147483647, Fallback = False, PrimaryNotFound = ReturnTypeDefault, SecondaryNotFound = ReturnTypeDefault, Key =  }.",
                     result.Exception!.Message);
    }

    [Fact]
    public async Task NotFoundBehavior_Unknown_FromContext_Throws()
    {
        // Arrange
        IPipeline pipeline = PipelineBuilder.Create()
            .NewPipeline()
            .Execute(static ([ResolveFrom(PrimarySource = Source.Context, Fallback = false, PrimaryNotFound = (NotFoundBehavior)int.MaxValue)] ITestStub x) =>
            {
                Assert.Null(x);
                return Task.CompletedTask;
            })
            .Build()
            .Pipelines[0];

        // Act
        PipelineRunResult result = await pipeline.RunAsync();

        // Assert
        Assert.False(result.IsSuccessful);

        Assert.IsType<ArgumentException>(result.Exception);
        Assert.Equal("NotFoundBehavior enum value 2147483647 is not supported. ResolveFromAttribute { PrimarySource = Context, Fallback = False, PrimaryNotFound = 2147483647, SecondaryNotFound = ReturnTypeDefault, Key =  }",
                     result.Exception!.Message);
    }

    [Fact]
    public async Task NotFoundBehavior_Unknown_FromServices_Throws()
    {
        // Arrange
        IPipeline pipeline = PipelineBuilder.Create()
            .NewPipeline()
            .Execute(static ([ResolveFrom(PrimarySource = Source.Services, Fallback = false, PrimaryNotFound = (NotFoundBehavior)int.MaxValue)] ITestStub x) =>
            {
                Assert.Null(x);
                return Task.CompletedTask;
            })
            .Build()
            .Pipelines[0];

        // Act
        PipelineRunResult result = await pipeline.RunAsync();

        // Assert
        Assert.False(result.IsSuccessful);

        Assert.IsType<ArgumentException>(result.Exception);
        Assert.Equal("NotFoundBehavior enum value 2147483647 is not supported. ResolveFromAttribute { PrimarySource = Services, Fallback = False, PrimaryNotFound = 2147483647, SecondaryNotFound = ReturnTypeDefault, Key =  }",
                     result.Exception!.Message);
    }
}
