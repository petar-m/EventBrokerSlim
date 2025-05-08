using System;
using System.Linq;
using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FuncPipeline.Tests;

public class PipelineBuilderTests
{
    [Fact]
    public void Build_Pipeline_Single_Function()
    {
        var pipelineBuilder = PipelineBuilder.Create()
              .NewPipeline()
              .Execute((int i) => Task.FromResult(i))
              .Build();

        Assert.Single(pipelineBuilder.Pipelines);
    }

    [Fact]
    public void Build_Pipeline_Multiple_Functions()
    {
        var pipelineBuilder = PipelineBuilder.Create()
              .NewPipeline()
              .Execute((int i) => Task.FromResult(i))
              .Execute((string s, INext next) => next.RunAsync())
              .Execute((bool b, INext next) => next.RunAsync())
              .Build();

        Assert.Single(pipelineBuilder.Pipelines);
    }

    [Fact]
    public void Build_Pipeline_With_ServiceProvider()
    {
        var serviceScopeFactory = A.Fake<IServiceScopeFactory>(x => x.Strict());

        var pipelineBuilder = PipelineBuilder.Create(serviceScopeFactory)
              .NewPipeline()
              .Execute((int i) => Task.FromResult(i))
              .Build();

        Assert.Single(pipelineBuilder.Pipelines);
        Assert.Equal(serviceScopeFactory, pipelineBuilder.Pipelines[0].ServiceScopeFactory);
    }


    [Fact]
    public void OnBuild_Callback_Is_Called()
    {
        object? setInCallback = null;

        var pipelineBuilder = PipelineBuilder.Create()
              .NewPipeline()
              .Execute((int i) => Task.FromResult(i))
              .Build(x => setInCallback = x);

        Assert.NotNull(setInCallback);
        Assert.IsType<IPipeline>(setInCallback, exactMatch: false);
    }
}
