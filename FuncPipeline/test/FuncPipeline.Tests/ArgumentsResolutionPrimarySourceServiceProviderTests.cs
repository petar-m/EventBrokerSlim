using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FuncPipeline.Tests;
public class ArgumentsResolutionPrimarySourceServiceProviderTests
{
    [Fact]
    public async Task Found_Attribute()
    {
        // Arrange
        ITestStub contextFunc = A.Fake<ITestStub>(x => x.Strict());
        A.CallTo(() => contextFunc.ExecuteAsync(default))
            .Returns(Task.CompletedTask);

        var serviceProvider = new ServiceCollection()
            .AddSingleton<ITestStub>(contextFunc)
            .BuildServiceProvider();

        IPipeline pipeline = PipelineBuilder.Create(serviceProvider.GetRequiredService<IServiceScopeFactory>())
            .NewPipeline()
            .Execute(static async ([ResolveFrom(PrimarySource = Source.Services, Fallback = false, PrimaryNotFound = NotFoundBehavior.ThrowException, SecondaryNotFound = NotFoundBehavior.ThrowException)] ITestStub x) =>
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
    public async Task Found_Fallback_Attribute()
    {
        // Arrange
        ITestStub contextFunc = A.Fake<ITestStub>(x => x.Strict());
        A.CallTo(() => contextFunc.ExecuteAsync(default))
            .Returns(Task.CompletedTask);

        var serviceProvider = new ServiceCollection()
            .AddSingleton<ITestStub>(contextFunc)
            .BuildServiceProvider();

        IPipeline pipeline = PipelineBuilder.Create(serviceProvider.GetRequiredService<IServiceScopeFactory>())
            .NewPipeline()
            .Execute(static async ([ResolveFrom(PrimarySource = Source.Services, Fallback = true, PrimaryNotFound = NotFoundBehavior.ReturnTypeDefault, SecondaryNotFound = NotFoundBehavior.ThrowException)] ITestStub x) =>
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
    public async Task Found_Keyed_Attribute()
    {
        // Arrange
        ITestStub contextFunc = A.Fake<ITestStub>(x => x.Strict());
        A.CallTo(() => contextFunc.ExecuteAsync(default))
            .Returns(Task.CompletedTask);

        var serviceProvider = new ServiceCollection()
            .AddKeyedSingleton<ITestStub>("service key", contextFunc)
            .BuildServiceProvider();

        IPipeline pipeline = PipelineBuilder.Create(serviceProvider.GetRequiredService<IServiceScopeFactory>())
            .NewPipeline()
            .Execute(static async ([ResolveFrom(PrimarySource = Source.Services, Fallback = false, PrimaryNotFound = NotFoundBehavior.ThrowException, SecondaryNotFound = NotFoundBehavior.ThrowException, Key = "service key")] ITestStub x) =>
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
    public async Task Found_AttributeAsParameter()
    {
        // Arrange
        ITestStub contextFunc = A.Fake<ITestStub>(x => x.Strict());
        A.CallTo(() => contextFunc.ExecuteAsync(default))
            .Returns(Task.CompletedTask);

        var serviceProvider = new ServiceCollection()
            .AddSingleton<ITestStub>(contextFunc)
            .BuildServiceProvider();

        IPipeline pipeline = PipelineBuilder.Create(serviceProvider.GetRequiredService<IServiceScopeFactory>())
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
                        PrimarySource = Source.Services,
                        Fallback = false,
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
        Assert.True(result.IsSuccessful);

        A.CallTo(() => contextFunc.ExecuteAsync(default))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Found_Keyed_AttributeAsParameter()
    {
        // Arrange
        ITestStub contextFunc = A.Fake<ITestStub>(x => x.Strict());
        A.CallTo(() => contextFunc.ExecuteAsync(default))
            .Returns(Task.CompletedTask);

        var serviceProvider = new ServiceCollection()
            .AddKeyedSingleton<ITestStub>("service key", contextFunc)
            .BuildServiceProvider();

        IPipeline pipeline = PipelineBuilder.Create(serviceProvider.GetRequiredService<IServiceScopeFactory>())
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
                        PrimarySource = Source.Services,
                        Fallback = false,
                        PrimaryNotFound = NotFoundBehavior.ReturnTypeDefault,
                        SecondaryNotFound = NotFoundBehavior.ThrowException,
                        Key = "service key"
                    }
                }
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
    public async Task NoFallback_ReturnTypeDefault_Attribute()
    {
        // Arrange
        IPipeline pipeline = PipelineBuilder.Create()
            .NewPipeline()
            .Execute(static ([ResolveFrom(PrimarySource = Source.Services, Fallback = false, PrimaryNotFound = NotFoundBehavior.ReturnTypeDefault, SecondaryNotFound = NotFoundBehavior.ThrowException)] ITestStub x) =>
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
            .Execute(static ([ResolveFrom(PrimarySource = Source.Services, Fallback = false, PrimaryNotFound = NotFoundBehavior.ReturnTypeDefault, SecondaryNotFound = NotFoundBehavior.ThrowException)] ITestStub x) =>
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
                        PrimarySource = Source.Services,
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
    public async Task NoFallback_Keyed_ReturnTypeDefault_Attribute()
    {
        // Arrange
        IPipeline pipeline = PipelineBuilder.Create()
            .NewPipeline()
            .Execute(static ([ResolveFrom(PrimarySource = Source.Services, Fallback = false, PrimaryNotFound = NotFoundBehavior.ReturnTypeDefault, SecondaryNotFound = NotFoundBehavior.ThrowException, Key = "service key")] ITestStub x) =>
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
    public async Task NoFallback_Keyed_ReturnTypeDefault_AttributeAsParameter()
    {
        // Arrange
        IPipeline pipeline = PipelineBuilder.Create()
            .NewPipeline()
            .Execute(static ([ResolveFrom(PrimarySource = Source.Services, Fallback = false, PrimaryNotFound = NotFoundBehavior.ReturnTypeDefault, SecondaryNotFound = NotFoundBehavior.ThrowException)] ITestStub x) =>
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
                        PrimarySource = Source.Services,
                        Fallback = false,
                        PrimaryNotFound = NotFoundBehavior.ReturnTypeDefault,
                        Key = "service key"
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
    public async Task NoFallback_ThrowException_Attribute()
    {
        // Arrange
        var serviceProvider = new ServiceCollection().BuildServiceProvider();

        IPipeline pipeline = PipelineBuilder.Create(serviceProvider.GetRequiredService<IServiceScopeFactory>())
            .NewPipeline()
            .Execute(static ([ResolveFrom(PrimarySource = Source.Services, Fallback = false, PrimaryNotFound = NotFoundBehavior.ThrowException, SecondaryNotFound = NotFoundBehavior.ThrowException)] ITestStub x) =>
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
        Assert.Equal(
            "No service for type FuncPipeline.Tests.ITestStub has been registered. ResolveFromAttribute { PrimarySource = Services, Fallback = False, PrimaryNotFound = ThrowException, SecondaryNotFound = ThrowException, Key =  }.",
            result.Exception.Message);
    }

    [Fact]
    public async Task NoFallback_ThrowException_AttributeAsParameter()
    {
        // Arrange
        var serviceProvider = new ServiceCollection().BuildServiceProvider();

        IPipeline pipeline = PipelineBuilder.Create(serviceProvider.GetRequiredService<IServiceScopeFactory>())
            .NewPipeline()
            .Execute(static ([ResolveFrom(PrimarySource = Source.Services, Fallback = false, PrimaryNotFound = NotFoundBehavior.ReturnTypeDefault, SecondaryNotFound = NotFoundBehavior.ThrowException)] ITestStub x) =>
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
                        PrimarySource = Source.Services,
                        Fallback = false,
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
            "No service for type FuncPipeline.Tests.ITestStub has been registered. ResolveFromAttribute { PrimarySource = Services, Fallback = False, PrimaryNotFound = ThrowException, SecondaryNotFound = ThrowException, Key =  }.",
            result.Exception.Message);
    }

    [Fact]
    public async Task NoFallback_Keyed_ThrowException_Attribute()
    {
        // Arrange
        var serviceProvider = new ServiceCollection().BuildServiceProvider();

        IPipeline pipeline = PipelineBuilder.Create(serviceProvider.GetRequiredService<IServiceScopeFactory>())
            .NewPipeline()
            .Execute(static ([ResolveFrom(PrimarySource = Source.Services, Fallback = false, PrimaryNotFound = NotFoundBehavior.ThrowException, SecondaryNotFound = NotFoundBehavior.ThrowException, Key = "service key")] ITestStub x) =>
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
        Assert.Equal(
            "No service for type FuncPipeline.Tests.ITestStub has been registered with key service key. ResolveFromAttribute { PrimarySource = Services, Fallback = False, PrimaryNotFound = ThrowException, SecondaryNotFound = ThrowException, Key = service key }.",
            result.Exception.Message);
    }

    [Fact]
    public async Task NoFallback_Keyed_ThrowException_AttributeAsParameter()
    {
        // Arrange
        var serviceProvider = new ServiceCollection().BuildServiceProvider();

        IPipeline pipeline = PipelineBuilder.Create(serviceProvider.GetRequiredService<IServiceScopeFactory>())
            .NewPipeline()
            .Execute(static ([ResolveFrom(PrimarySource = Source.Services, Fallback = false, PrimaryNotFound = NotFoundBehavior.ReturnTypeDefault, SecondaryNotFound = NotFoundBehavior.ThrowException)] ITestStub x) =>
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
                        PrimarySource = Source.Services,
                        Fallback = false,
                        PrimaryNotFound = NotFoundBehavior.ThrowException,
                        SecondaryNotFound = NotFoundBehavior.ThrowException,
                        Key = "service key"
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
            "No service for type FuncPipeline.Tests.ITestStub has been registered with key service key. ResolveFromAttribute { PrimarySource = Services, Fallback = False, PrimaryNotFound = ThrowException, SecondaryNotFound = ThrowException, Key = service key }.",
            result.Exception.Message);
    }

    [Fact]
    public async Task NoFallback_ServiceProvider_Null_Throws()
    {
        // Arrange
        IPipeline pipeline = PipelineBuilder.Create()
            .NewPipeline()
            .Execute(static async ([ResolveFrom(PrimarySource = Source.Services, Fallback = false, PrimaryNotFound = NotFoundBehavior.ThrowException, SecondaryNotFound = NotFoundBehavior.ThrowException)] ITestStub x) =>
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
            "IPipeline.ServiceProvider is null. Cannot resolve parameter of type FuncPipeline.Tests.ITestStub. ResolveFromAttribute { PrimarySource = Services, Fallback = False, PrimaryNotFound = ThrowException, SecondaryNotFound = ThrowException, Key =  }",
            result.Exception.Message);
    }

    [Fact]
    public async Task Fallback_Found_Attribute()
    {
        // Arrange
        ITestStub contextFunc = A.Fake<ITestStub>(x => x.Strict());
        A.CallTo(() => contextFunc.ExecuteAsync(default))
            .Returns(Task.CompletedTask);

        var serviceProvider = new ServiceCollection().BuildServiceProvider();

        IPipeline pipeline = PipelineBuilder.Create(serviceProvider.GetRequiredService<IServiceScopeFactory>())
            .NewPipeline()
            .Execute(static async ([ResolveFrom(PrimarySource = Source.Services, Fallback = true, PrimaryNotFound = NotFoundBehavior.ThrowException, SecondaryNotFound = NotFoundBehavior.ThrowException)] ITestStub x) =>
            {
                await x.ExecuteAsync(default);
            })
            .Build()
            .Pipelines[0];

        var context = new PipelineRunContext().Set(typeof(ITestStub), contextFunc);

        // Act
        PipelineRunResult result = await pipeline.RunAsync(context);

        // Assert
        Assert.True(result.IsSuccessful);

        A.CallTo(() => contextFunc.ExecuteAsync(default))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Fallback_Found_AttributeAsParameter()
    {
        // Arrange
        ITestStub contextFunc = A.Fake<ITestStub>(x => x.Strict());
        A.CallTo(() => contextFunc.ExecuteAsync(default))
            .Returns(Task.CompletedTask);

        var serviceProvider = new ServiceCollection().BuildServiceProvider();

        IPipeline pipeline = PipelineBuilder.Create(serviceProvider.GetRequiredService<IServiceScopeFactory>())
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
                        PrimarySource = Source.Services,
                        Fallback = true,
                        PrimaryNotFound = NotFoundBehavior.ThrowException,
                        SecondaryNotFound = NotFoundBehavior.ThrowException
                    }
                }
            })
            .Build()
            .Pipelines[0];

        var context = new PipelineRunContext().Set(typeof(ITestStub), contextFunc);

        // Act
        PipelineRunResult result = await pipeline.RunAsync(context);

        // Assert
        Assert.True(result.IsSuccessful);

        A.CallTo(() => contextFunc.ExecuteAsync(default))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Fallback_NotFound_ReturnTypeDefault_Attribute()
    {
        // Arrange
        var serviceProvider = new ServiceCollection().BuildServiceProvider();

        IPipeline pipeline = PipelineBuilder.Create(serviceProvider.GetRequiredService<IServiceScopeFactory>())
            .NewPipeline()
            .Execute(static ([ResolveFrom(PrimarySource = Source.Services, Fallback = true, PrimaryNotFound = NotFoundBehavior.ThrowException, SecondaryNotFound = NotFoundBehavior.ReturnTypeDefault)] ITestStub x) =>
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
        var serviceProvider = new ServiceCollection().BuildServiceProvider();

        IPipeline pipeline = PipelineBuilder.Create(serviceProvider.GetRequiredService<IServiceScopeFactory>())
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
                        PrimarySource = Source.Services,
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
        var serviceProvider = new ServiceCollection().BuildServiceProvider();

        IPipeline pipeline = PipelineBuilder.Create(serviceProvider.GetRequiredService<IServiceScopeFactory>())
            .NewPipeline()
            .Execute(static ([ResolveFrom(PrimarySource = Source.Services, Fallback = true, PrimaryNotFound = NotFoundBehavior.ThrowException, SecondaryNotFound = NotFoundBehavior.ThrowException)] ITestStub x) =>
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
        Assert.Equal("No FuncPipeline.Tests.ITestStub found in PipelineRunContext. ResolveFromAttribute { PrimarySource = Services, Fallback = True, PrimaryNotFound = ThrowException, SecondaryNotFound = ThrowException, Key =  }",
             result.Exception.Message);
    }

    [Fact]
    public async Task Fallback_NotFound_ThrowException_AttributeAsParameter()
    {
        // Arrange
        var serviceProvider = new ServiceCollection().BuildServiceProvider();

        IPipeline pipeline = PipelineBuilder.Create(serviceProvider.GetRequiredService<IServiceScopeFactory>())
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
                        PrimarySource = Source.Services,
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
        Assert.Equal("No FuncPipeline.Tests.ITestStub found in PipelineRunContext. ResolveFromAttribute { PrimarySource = Services, Fallback = True, PrimaryNotFound = ThrowException, SecondaryNotFound = ThrowException, Key =  }",
             result.Exception.Message);
    }

}
