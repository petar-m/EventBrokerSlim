using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Enfolder.Tests;

public class ArgumentsResolutionPrimarySourceContextTests
{
    [Fact]
    public async Task NoFallback_Throw_Attribute()
    {
        // Arrange
        ITestStub contextFunc = A.Fake<ITestStub>(x => x.Strict());
        A.CallTo(() => contextFunc.ExecuteAsync(default))
            .Returns(Task.CompletedTask);

        var serviceProvider = new ServiceCollection()
            .AddSingleton<ITestStub>(contextFunc)
            .BuildServiceProvider();

        IPipeline pipeline = PipelineBuilder.Create(serviceProvider)
            .NewPipeline()
            .Execute(static async ([ResolveFrom(PrimarySource = Source.Context, Fallback = false, PrimaryNotFound = NotFoundBehavior.ThrowException)] ITestStub x) =>
            {
                await x.ExecuteAsync(default);
            })
            .Build()
            .Pipelines[0];

        var context = new PipelineRunContext();

        // Act
        PipelineRunResult result = await pipeline.RunAsync(context);

        // Assert
        Assert.False(result.IsSuccessful);

        A.CallTo(() => contextFunc.ExecuteAsync(default))
            .MustNotHaveHappened();

        Assert.IsType<ArgumentException>(result.Exception);
        Assert.Equal("No Enfolder.Tests.ITestStub found in PipelineRunContext. ResolveFromAttribute { PrimarySource = Context, Fallback = False, PrimaryNotFound = ThrowException, SecondaryNotFound = ReturnTypeDefault, Key =  }",
                     result.Exception.Message);
    }

    [Fact]
    public async Task NoFallback_Throw_AttributeAsParameter()
    {
        // Arrange
        ITestStub contextFunc = A.Fake<ITestStub>(x => x.Strict());
        A.CallTo(() => contextFunc.ExecuteAsync(default))
            .Returns(Task.CompletedTask);

        var serviceProvider = new ServiceCollection()
            .AddSingleton<ITestStub>(contextFunc)
            .BuildServiceProvider();

        IPipeline pipeline = PipelineBuilder.Create(serviceProvider)
            .NewPipeline()
            .Execute(static async (ITestStub x) =>
            {
                await x.ExecuteAsync(default);
            },
            new Dictionary<int, ResolveFromAttribute>
            {
                {
                    0,
                    new ResolveFromAttribute
                    {
                        PrimarySource = Source.Context,
                        Fallback = false,
                        PrimaryNotFound = NotFoundBehavior.ThrowException
                    }
                }
            })
            .Build()
            .Pipelines[0];

        var context = new PipelineRunContext();

        // Act
        PipelineRunResult result = await pipeline.RunAsync(context);

        // Assert
        Assert.False(result.IsSuccessful);

        A.CallTo(() => contextFunc.ExecuteAsync(default))
            .MustNotHaveHappened();

        Assert.IsType<ArgumentException>(result.Exception);
        Assert.Equal("No Enfolder.Tests.ITestStub found in PipelineRunContext. ResolveFromAttribute { PrimarySource = Context, Fallback = False, PrimaryNotFound = ThrowException, SecondaryNotFound = ReturnTypeDefault, Key =  }",
                     result.Exception.Message);
    }

    [Fact]
    public async Task NoFallback_ReturnTypeDefault_Attribute()
    {
        // Arrange
        IPipeline pipeline = PipelineBuilder.Create()
            .NewPipeline()
            .Execute(static ([ResolveFrom(PrimarySource = Source.Context, Fallback = false, PrimaryNotFound = NotFoundBehavior.ReturnTypeDefault)] ITestStub x) =>
            {
                Assert.Null(x);
                return Task.CompletedTask;
            })
            .Build()
            .Pipelines[0];

        // Act
        PipelineRunResult result = await pipeline.RunAsync();

        // Assert
        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public async Task NoFallback_ReturnTypeDefault_AttributeAsParameter()
    {
        // Arrange
        IPipeline pipeline = PipelineBuilder.Create()
            .NewPipeline()
            .Execute(static (ITestStub x) =>
            {
                Assert.Null(x);
                return Task.CompletedTask;
            },
            new Dictionary<int, ResolveFromAttribute>
            {
                {
                    0,
                    new ResolveFromAttribute
                    {
                        PrimarySource = Source.Context,
                        Fallback = false,
                        PrimaryNotFound = NotFoundBehavior.ReturnTypeDefault
                    }
                }
            })
            .Build()
            .Pipelines[0];

        // Act
        PipelineRunResult result = await pipeline.RunAsync();

        // Assert
        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public async Task Found()
    {
        // Arrange
        ITestStub contextFunc = A.Fake<ITestStub>(x => x.Strict());
        A.CallTo(() => contextFunc.ExecuteAsync(default))
            .Returns(Task.CompletedTask);

        var serviceProvider = new ServiceCollection()
            .AddSingleton<ITestStub>(contextFunc)
            .BuildServiceProvider();

        IPipeline pipeline = PipelineBuilder.Create(serviceProvider)
            .NewPipeline()
            .Execute(static async ([ResolveFrom(PrimarySource = Source.Context, Fallback = false, PrimaryNotFound = NotFoundBehavior.ThrowException)] ITestStub x) =>
            {
                await x.ExecuteAsync(default);
            })
            .Build()
            .Pipelines[0];

        var context = new PipelineRunContext().Set<ITestStub>(contextFunc);

        // Act
        PipelineRunResult result = await pipeline.RunAsync(context);

        // Assert
        Assert.True(result.IsSuccessful);

        A.CallTo(() => contextFunc.ExecuteAsync(default))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Fallback_Found_Attribute()
    {
        // Arrange
        ITestStub contextFunc = A.Fake<ITestStub>(x => x.Strict());
        A.CallTo(() => contextFunc.ExecuteAsync(default))
            .Returns(Task.CompletedTask);

        var serviceProvider = new ServiceCollection()
            .AddSingleton<ITestStub>(contextFunc)
            .BuildServiceProvider();

        IPipeline pipeline = PipelineBuilder.Create(serviceProvider)
            .NewPipeline()
            .Execute(static async ([ResolveFrom(PrimarySource = Source.Context, Fallback = true, PrimaryNotFound = NotFoundBehavior.ThrowException, SecondaryNotFound = NotFoundBehavior.ThrowException)] ITestStub x) =>
            {
                await x.ExecuteAsync(default);
            })
            .Build()
            .Pipelines[0];

        // Act
        PipelineRunResult result = await pipeline.RunAsync();

        // Assert
        Assert.True(result.IsSuccessful);

        A.CallTo(() => contextFunc.ExecuteAsync(default))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Fallback_NotFound_ReturnTypeDefault_Attribute()
    {
        // Arrange
        ITestStub contextFunc = A.Fake<ITestStub>(x => x.Strict());
        A.CallTo(() => contextFunc.ExecuteAsync(default))
            .Returns(Task.CompletedTask);

        var serviceProvider = new ServiceCollection()
            .BuildServiceProvider();

        IPipeline pipeline = PipelineBuilder.Create(serviceProvider)
            .NewPipeline()
            .Execute(static ([ResolveFrom(PrimarySource = Source.Context, Fallback = true, PrimaryNotFound = NotFoundBehavior.ThrowException, SecondaryNotFound = NotFoundBehavior.ReturnTypeDefault)] ITestStub x) =>
            {
                Assert.Null(x);
                return Task.CompletedTask;
            })
            .Build()
            .Pipelines[0];

        // Act
        PipelineRunResult result = await pipeline.RunAsync();

        // Assert
        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public async Task Fallback_NotFound_ReturnTypeDefault_AttributeAsParameter()
    {
        // Arrange
        ITestStub contextFunc = A.Fake<ITestStub>(x => x.Strict());
        A.CallTo(() => contextFunc.ExecuteAsync(default))
            .Returns(Task.CompletedTask);

        var serviceProvider = new ServiceCollection()
            .BuildServiceProvider();

        IPipeline pipeline = PipelineBuilder.Create(serviceProvider)
            .NewPipeline()
            .Execute(static (ITestStub x) =>
            {
                Assert.Null(x);
                return Task.CompletedTask;
            },
            new Dictionary<int, ResolveFromAttribute>
            {
                {
                    0,
                    new ResolveFromAttribute
                    {
                        PrimarySource = Source.Context,
                        Fallback = true,
                        PrimaryNotFound = NotFoundBehavior.ThrowException,
                        SecondaryNotFound = NotFoundBehavior.ReturnTypeDefault
                    }
                }
            })
            .Build()
            .Pipelines[0];

        // Act
        PipelineRunResult result = await pipeline.RunAsync();

        // Assert
        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public async Task Fallback_NotFound_ThrowException_Attribute()
    {
        // Arrange
        ITestStub contextFunc = A.Fake<ITestStub>(x => x.Strict());
        A.CallTo(() => contextFunc.ExecuteAsync(default))
            .Returns(Task.CompletedTask);

        var serviceProvider = new ServiceCollection()
            .BuildServiceProvider();

        IPipeline pipeline = PipelineBuilder.Create(serviceProvider)
            .NewPipeline()
            .Execute(async static ([ResolveFrom(PrimarySource = Source.Context, Fallback = true, PrimaryNotFound = NotFoundBehavior.ThrowException, SecondaryNotFound = NotFoundBehavior.ThrowException)] ITestStub x) =>
            {
                await x.ExecuteAsync(default);
            })
            .Build()
            .Pipelines[0];

        // Act
        PipelineRunResult result = await pipeline.RunAsync();

        // Assert
        Assert.False(result.IsSuccessful);

        Assert.IsType<ArgumentException>(result.Exception);
        Assert.Equal(
            "No service for type Enfolder.Tests.ITestStub has been registered. ResolveFromAttribute { PrimarySource = Context, Fallback = True, PrimaryNotFound = ThrowException, SecondaryNotFound = ThrowException, Key =  }.",
            result.Exception.Message);
    }

    [Fact]
    public async Task Fallback_NotFound_ThrowException_AttributeAsParameter()
    {
        // Arrange
        ITestStub contextFunc = A.Fake<ITestStub>(x => x.Strict());
        A.CallTo(() => contextFunc.ExecuteAsync(default))
            .Returns(Task.CompletedTask);

        var serviceProvider = new ServiceCollection()
            .BuildServiceProvider();

        IPipeline pipeline = PipelineBuilder.Create(serviceProvider)
            .NewPipeline()
            .Execute(async static (ITestStub x) =>
            {
                await x.ExecuteAsync(default);
            },
            new Dictionary<int, ResolveFromAttribute>
            {
                {
                    0,
                    new ResolveFromAttribute
                    {
                        PrimarySource = Source.Context,
                        Fallback = true,
                        PrimaryNotFound = NotFoundBehavior.ThrowException,
                        SecondaryNotFound = NotFoundBehavior.ThrowException
                    }
                }
            })
            .Build()
            .Pipelines[0];

        // Act
        PipelineRunResult result = await pipeline.RunAsync();

        // Assert
        Assert.False(result.IsSuccessful);

        Assert.IsType<ArgumentException>(result.Exception);
        Assert.Equal(
            "No service for type Enfolder.Tests.ITestStub has been registered. ResolveFromAttribute { PrimarySource = Context, Fallback = True, PrimaryNotFound = ThrowException, SecondaryNotFound = ThrowException, Key =  }.",
            result.Exception.Message);
    }

    [Fact]
    public async Task Fallback_NotFound_ThrowException_ServiceProvider_Null()
    {
        // Arrange
        ITestStub contextFunc = A.Fake<ITestStub>(x => x.Strict());
        A.CallTo(() => contextFunc.ExecuteAsync(default))
            .Returns(Task.CompletedTask);

        IPipeline pipeline = PipelineBuilder.Create()
            .NewPipeline()
            .Execute(async static ([ResolveFrom(PrimarySource = Source.Context, Fallback = true, PrimaryNotFound = NotFoundBehavior.ThrowException, SecondaryNotFound = NotFoundBehavior.ThrowException)] ITestStub x) =>
            {
                await x.ExecuteAsync(default);
            })
            .Build()
            .Pipelines[0];

        // Act
        PipelineRunResult result = await pipeline.RunAsync();

        // Assert
        Assert.False(result.IsSuccessful);

        Assert.IsType<ArgumentException>(result.Exception);
        Assert.Equal(
            "IPipeline.ServiceProvider is null. Cannot resolve parameter of type Enfolder.Tests.ITestStub. ResolveFromAttribute { PrimarySource = Context, Fallback = True, PrimaryNotFound = ThrowException, SecondaryNotFound = ThrowException, Key =  }",
            result.Exception.Message);
    }

    [Fact]
    public async Task Fallback_NotFound_ReturnTypeDefault_ServiceProvider_Null()
    {
        // Arrange
        ITestStub contextFunc = A.Fake<ITestStub>(x => x.Strict());
        A.CallTo(() => contextFunc.ExecuteAsync(default))
            .Returns(Task.CompletedTask);

        IPipeline pipeline = PipelineBuilder.Create()
            .NewPipeline()
            .Execute(static ([ResolveFrom(PrimarySource = Source.Context, Fallback = true, PrimaryNotFound = NotFoundBehavior.ThrowException, SecondaryNotFound = NotFoundBehavior.ReturnTypeDefault)] ITestStub x) =>
            {
                Assert.Null(x);
                return Task.CompletedTask;
            })
            .Build()
            .Pipelines[0];

        // Act
        PipelineRunResult result = await pipeline.RunAsync();

        // Assert
        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public async Task Fallback_Keyed_NotFound_ReturnTypeDefault_Attribute()
    {
        // Arrange
        ITestStub contextFunc = A.Fake<ITestStub>(x => x.Strict());
        A.CallTo(() => contextFunc.ExecuteAsync(default))
            .Returns(Task.CompletedTask);

        var serviceProvider = new ServiceCollection()
            .BuildServiceProvider();

        IPipeline pipeline = PipelineBuilder.Create(serviceProvider)
            .NewPipeline()
            .Execute(static ([ResolveFrom(PrimarySource = Source.Context, Fallback = true, PrimaryNotFound = NotFoundBehavior.ThrowException, SecondaryNotFound = NotFoundBehavior.ReturnTypeDefault, Key = "service_key")] ITestStub x) =>
            {
                Assert.Null(x);
                return Task.CompletedTask;
            })
            .Build()
            .Pipelines[0];

        // Act
        PipelineRunResult result = await pipeline.RunAsync();

        // Assert
        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public async Task Fallback_Keyed_NotFound_ReturnTypeDefault_AttributeAsParameter()
    {
        // Arrange
        ITestStub contextFunc = A.Fake<ITestStub>(x => x.Strict());
        A.CallTo(() => contextFunc.ExecuteAsync(default))
            .Returns(Task.CompletedTask);

        var serviceProvider = new ServiceCollection()
            .BuildServiceProvider();

        IPipeline pipeline = PipelineBuilder.Create(serviceProvider)
            .NewPipeline()
            .Execute(static (ITestStub x) =>
            {
                Assert.Null(x);
                return Task.CompletedTask;
            },
            new Dictionary<int, ResolveFromAttribute>
            {
                {
                    0,
                    new ResolveFromAttribute
                    {
                        PrimarySource = Source.Context,
                        Fallback = true,
                        PrimaryNotFound = NotFoundBehavior.ThrowException,
                        SecondaryNotFound = NotFoundBehavior.ReturnTypeDefault,
                        Key = "service_key"
                    }
                }
            })
            .Build()
            .Pipelines[0];

        // Act
        PipelineRunResult result = await pipeline.RunAsync();

        // Assert
        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public async Task Fallback_Keyed_Found_Attribute()
    {
        // Arrange
        ITestStub contextFunc = A.Fake<ITestStub>(x => x.Strict());
        A.CallTo(() => contextFunc.ExecuteAsync(default))
            .Returns(Task.CompletedTask);

        var serviceProvider = new ServiceCollection()
            .AddKeyedSingleton<ITestStub>("service_key", contextFunc)
            .BuildServiceProvider();

        IPipeline pipeline = PipelineBuilder.Create(serviceProvider)
            .NewPipeline()
            .Execute(static async ([ResolveFrom(PrimarySource = Source.Context, Fallback = true, PrimaryNotFound = NotFoundBehavior.ThrowException, SecondaryNotFound = NotFoundBehavior.ThrowException, Key = "service_key")] ITestStub x) =>
            {
                await x.ExecuteAsync(default);
            })
            .Build()
            .Pipelines[0];

        // Act
        PipelineRunResult result = await pipeline.RunAsync();

        // Assert
        Assert.True(result.IsSuccessful);

        A.CallTo(() => contextFunc.ExecuteAsync(default))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Fallback_Keyed_NotFound_ThrowException_Attribute()
    {
        // Arrange
        ITestStub contextFunc = A.Fake<ITestStub>(x => x.Strict());
        A.CallTo(() => contextFunc.ExecuteAsync(default))
            .Returns(Task.CompletedTask);

        var serviceProvider = new ServiceCollection()
            .BuildServiceProvider();

        IPipeline pipeline = PipelineBuilder.Create(serviceProvider)
            .NewPipeline()
            .Execute(async static ([ResolveFrom(PrimarySource = Source.Context, Fallback = true, PrimaryNotFound = NotFoundBehavior.ThrowException, SecondaryNotFound = NotFoundBehavior.ThrowException, Key = "service_key")] ITestStub x) =>
            {
                await x.ExecuteAsync(default);
            })
            .Build()
            .Pipelines[0];

        // Act
        PipelineRunResult result = await pipeline.RunAsync();

        // Assert
        Assert.False(result.IsSuccessful);

        Assert.IsType<ArgumentException>(result.Exception);
        Assert.Equal(
            "No service for type Enfolder.Tests.ITestStub has been registered with key service_key. ResolveFromAttribute { PrimarySource = Context, Fallback = True, PrimaryNotFound = ThrowException, SecondaryNotFound = ThrowException, Key = service_key }.",
            result.Exception.Message);
    }

    [Fact]
    public async Task Fallback_Keyed_NotFound_ThrowException_AttributeAsParameter()
    {
        // Arrange
        ITestStub contextFunc = A.Fake<ITestStub>(x => x.Strict());
        A.CallTo(() => contextFunc.ExecuteAsync(default))
            .Returns(Task.CompletedTask);

        var serviceProvider = new ServiceCollection()
            .BuildServiceProvider();

        IPipeline pipeline = PipelineBuilder.Create(serviceProvider)
            .NewPipeline()
            .Execute(async static (ITestStub x) =>
            {
                await x.ExecuteAsync(default);
            },
            new Dictionary<int, ResolveFromAttribute>
            {
               {
                   0,
                   new ResolveFromAttribute
                   {
                       PrimarySource = Source.Context,
                       Fallback = true,
                       PrimaryNotFound = NotFoundBehavior.ThrowException,
                       SecondaryNotFound = NotFoundBehavior.ThrowException,
                       Key = "service_key"
                   }
               }
            })
            .Build()
            .Pipelines[0];

        // Act
        PipelineRunResult result = await pipeline.RunAsync();

        // Assert
        Assert.False(result.IsSuccessful);

        Assert.IsType<ArgumentException>(result.Exception);
        Assert.Equal(
            "No service for type Enfolder.Tests.ITestStub has been registered with key service_key. ResolveFromAttribute { PrimarySource = Context, Fallback = True, PrimaryNotFound = ThrowException, SecondaryNotFound = ThrowException, Key = service_key }.",
            result.Exception.Message);
    }
}
