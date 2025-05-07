using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FuncPipeline.Tests;

public class ArgumentsResolutionServiceScopeOptionsTests
{
    [Fact]
    public async Task ServiceScopePerFunction_True()
    {
        var serviceProvider = new ServiceCollection()
            .AddScoped<ITestStub>(x => A.Fake<ITestStub>())
            .BuildServiceProvider();

        IPipeline pipeline = PipelineBuilder.Create(serviceProvider)
            .NewPipeline(new PipelineRunOptions { ServiceScopePerFunction = true })
            .Execute(static async (ITestStub x, PipelineRunContext context, INext next) =>
            {
                context.Set<(int Dependency1, int Dependency2)>((x.GetHashCode(), 0));
                await next.RunAsync();
            })
            .Execute((ITestStub x, PipelineRunContext context) =>
            {
                _ = context.TryGet<(int Dependency1, int Dependency2)>(out var dependencies);
                context.Set<(int Dependency1, int Dependency2)>((dependencies.Dependency1, x.GetHashCode()));
                return Task.CompletedTask;
            })
            .Build()
            .Pipelines[0];

        PipelineRunResult result = await pipeline.RunAsync();

        Assert.True(result.IsSuccessful);
        Assert.True(result.Context.TryGet<(int Dependency1, int Dependency2)>(out var dependencies));
        Assert.NotEqual(dependencies.Dependency1, dependencies.Dependency2);
    }

    [Fact]
    public async Task ServiceScopePerFunction_True_Is_Default()
    {
        var serviceProvider = new ServiceCollection()
            .AddScoped<ITestStub>(x => A.Fake<ITestStub>())
            .BuildServiceProvider();

        IPipeline pipeline = PipelineBuilder.Create(serviceProvider)
            .NewPipeline(new PipelineRunOptions { ServiceScopePerFunction = true })
            .Execute(static async (ITestStub x, PipelineRunContext context, INext next) =>
            {
                context.Set<(int Dependency1, int Dependency2)>((x.GetHashCode(), 0));
                await next.RunAsync();
            })
            .Execute((ITestStub x, PipelineRunContext context) =>
            {
                _ = context.TryGet<(int Dependency1, int Dependency2)>(out var dependencies);
                context.Set<(int Dependency1, int Dependency2)>((dependencies.Dependency1, x.GetHashCode()));
                return Task.CompletedTask;
            })
            .Build()
            .Pipelines[0];

        PipelineRunResult result = await pipeline.RunAsync();

        Assert.True(result.IsSuccessful);
        Assert.True(result.Context.TryGet<(int Dependency1, int Dependency2)>(out var dependencies));
        Assert.NotEqual(dependencies.Dependency1, dependencies.Dependency2);
    }

    [Fact]
    public async Task ServiceScopePerFunction_False()
    {
        var serviceProvider = new ServiceCollection()
            .AddScoped<ITestStub>(x => A.Fake<ITestStub>())
            .BuildServiceProvider();

        IPipeline pipeline = PipelineBuilder.Create(serviceProvider)
            .NewPipeline(new PipelineRunOptions { ServiceScopePerFunction = false })
            .Execute(static async (ITestStub x, PipelineRunContext context, INext next) =>
            {
                context.Set<(int Dependency1, int Dependency2)>((x.GetHashCode(), 0));
                await next.RunAsync();
            })
            .Execute((ITestStub x, PipelineRunContext context) =>
            {
                _ = context.TryGet<(int Dependency1, int Dependency2)>(out var dependencies);
                context.Set<(int Dependency1, int Dependency2)>((dependencies.Dependency1, x.GetHashCode()));
                return Task.CompletedTask;
            })
            .Build()
            .Pipelines[0];

        PipelineRunResult result = await pipeline.RunAsync();

        Assert.True(result.IsSuccessful);
        Assert.True(result.Context.TryGet<(int Dependency1, int Dependency2)>(out var dependencies));
        Assert.Equal(dependencies.Dependency1, dependencies.Dependency2);
    }

    [Fact]
    public async Task ServiceScopePerFunction_False_DoesNotThrow()
    {
        IPipeline pipeline = PipelineBuilder.Create()
            .NewPipeline(new PipelineRunOptions { ServiceScopePerFunction = false })
            .Execute(static (ITestStub x) =>
            {
                Assert.Null(x);
                return Task.CompletedTask;
            })
            .Build()
            .Pipelines[0];

        PipelineRunResult result = await pipeline.RunAsync();

        Assert.True(result.IsSuccessful, result.Exception?.Message);
    }
}
