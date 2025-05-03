using System;
using System.Threading.Tasks;
using FakeItEasy;
using Xunit;

namespace Enfolder.Tests;

public class PipelineBuilderTests
{
    [Fact]
    public void Build_Pipeline_Without_Wrappers()
    {
        var pipelineBuilder = PipelineBuilder.Create()
              .NewPipeline()
              .Execute((int i) => Task.FromResult(i))
              .Build();

        Assert.Single(pipelineBuilder.Pipelines);
        Assert.Fail("Test not implemented yet.");
    }

    [Fact]
    public void Build_Pipeline_With_Wrappers()
    {
        var pipelineBuilder = PipelineBuilder.Create()
              .NewPipeline()
              .Execute((int i) => Task.FromResult(i))
              .WrapWith((string s, INext next) => next.RunAsync())
              .WrapWith((bool b, INext next) => next.RunAsync())
              .Build();

        Assert.Fail();
        Assert.Single(pipelineBuilder.Pipelines);
    }

    [Fact]
    public void Build_Pipeline_With_ServiceProvider()
    {
        var serviceProvider = A.Fake<IServiceProvider>(x => x.Strict());

        var pipelineBuilder = PipelineBuilder.Create(serviceProvider)
              .NewPipeline()
              .Execute((int i) => Task.FromResult(i))
              .Build();

        Assert.Single(pipelineBuilder.Pipelines);
        Assert.Equal(serviceProvider, pipelineBuilder.Pipelines[0].ServiceProvider);
    }


    [Fact]
    public void Build_Multiple_Pipelines()
    {
        var pipelineBuilder = PipelineBuilder.Create()
              .NewPipeline()
              .Execute((int i) => Task.FromResult(i))
              .Build()
              .NewPipeline()
              .Execute((int i) => Task.FromResult(i))
              .WrapWith((string s, INext next) => next.RunAsync())
              .Build()
              .NewPipeline()
              .Execute((int i) => Task.FromResult(i))
              .WrapWith((string s, INext next) => next.RunAsync())
              .WrapWith((bool b, INext next) => next.RunAsync())
              .Build();

        Assert.Fail("Test not implemented yet.");
    }

    [Fact]
    public void Build_Multiple_Pipelines_For_Same_Key()
    {
        var pipelineBuilder = PipelineBuilder.Create()
              .NewPipeline()
              .Execute((int i) => Task.FromResult(i))
              .Build()
              .NewPipeline()
              .Execute((int i) => Task.FromResult(i))
              .WrapWith((string s, INext next) => next.RunAsync())
              .Build()
              .NewPipeline()
              .Execute((int i) => Task.FromResult(i))
              .WrapWith((string s, INext next) => next.RunAsync())
              .WrapWith((bool b, INext next) => next.RunAsync())
              .Build();

        Assert.Fail();
        Assert.Equal(3, pipelineBuilder.Pipelines.Count);
        
    }
}
