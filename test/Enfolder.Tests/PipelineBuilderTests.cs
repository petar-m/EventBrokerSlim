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
        var pipelineBuilder = new PipelineBuilder()
              .For("1")
              .Execute((int i) => Task.FromResult(i))
              .Build();

        Assert.Single(pipelineBuilder.Pipelines);
        Assert.Equal("1", pipelineBuilder.Pipelines[0].Key);
    }

    [Fact]
    public void Build_Pipeline_With_Wrappers()
    {
        var pipelineBuilder = new PipelineBuilder()
              .For("1")
              .Execute((int i) => Task.FromResult(i))
              .WrapWith((string s, INext next) => next.RunAsync())
              .WrapWith((bool b, INext next) => next.RunAsync())
              .Build();

        Assert.Single(pipelineBuilder.Pipelines);
        Assert.Equal("1", pipelineBuilder.Pipelines[0].Key);
    }

    [Fact]
    public void Build_Pipeline_With_ServiceProvider()
    {
        var serviceProvider = A.Fake<IServiceProvider>(x => x.Strict());

        var pipelineBuilder = new PipelineBuilder(serviceProvider)
              .For("1")
              .Execute((int i) => Task.FromResult(i))
              .Build();

        Assert.Single(pipelineBuilder.Pipelines);
        Assert.Equal(serviceProvider, pipelineBuilder.Pipelines[0].ServiceProvider);
    }

    [Fact]
    public void Build_Pipeline_Without_Type_Key()
    {
        var pipelineBuilder = new PipelineBuilder()
              .For<int>()
              .Execute((int i) => Task.FromResult(i))
              .Build();

        Assert.Single(pipelineBuilder.Pipelines);
        Assert.Equal(typeof(int).FullName, pipelineBuilder.Pipelines[0].Key);
    }


    [Fact]
    public void Build_Pipeline_With_Custom_Key()
    {
        var keyResolver = A.Fake<IPipelineKeyResolver>(x => x.Strict());
        var key = "customKey";
        A.CallTo(() => keyResolver.Key()).Returns(key);

        var pipelineBuilder = new PipelineBuilder()
              .For(keyResolver)
              .Execute((int i) => Task.FromResult(i))
              .Build();

        Assert.Single(pipelineBuilder.Pipelines);
        Assert.Equal(key, pipelineBuilder.Pipelines[0].Key);
    }

    [Fact]
    public void Build_Multiple_Pipelines()
    {
        var pipelineBuilder = new PipelineBuilder()
              .For("one")
              .Execute((int i) => Task.FromResult(i))
              .Build()
              .For("two")
              .Execute((int i) => Task.FromResult(i))
              .WrapWith((string s, INext next) => next.RunAsync())
              .Build()
              .For("three")
              .Execute((int i) => Task.FromResult(i))
              .WrapWith((string s, INext next) => next.RunAsync())
              .WrapWith((bool b, INext next) => next.RunAsync())
              .Build();

        Assert.Collection(
            pipelineBuilder.Pipelines,
            p => Assert.Equal("one", p.Key),
            p => Assert.Equal("two", p.Key),
            p => Assert.Equal("three", p.Key)
        );
    }

    [Fact]
    public void Build_Multiple_Pipelines_For_Same_Key()
    {
        var pipelineBuilder = new PipelineBuilder()
              .For("1")
              .Execute((int i) => Task.FromResult(i))
              .Build()
              .For("1")
              .Execute((int i) => Task.FromResult(i))
              .WrapWith((string s, INext next) => next.RunAsync())
              .Build()
              .For("1")
              .Execute((int i) => Task.FromResult(i))
              .WrapWith((string s, INext next) => next.RunAsync())
              .WrapWith((bool b, INext next) => next.RunAsync())
              .Build();

        Assert.Equal(3, pipelineBuilder.Pipelines.Count);
        Assert.All(pipelineBuilder.Pipelines, p => Assert.Equal("1", p.Key));
    }
}
