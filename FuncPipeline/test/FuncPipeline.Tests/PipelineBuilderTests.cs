using System;
using Xunit;

namespace FuncPipeline.Tests;

public class PipelineBuilderTests
{
    [Fact]
    public void WithName_produces_pipeline_with_name_set()
    {
        var name = "pipeline 1";

        IPipeline pipeline = new PipelineBuilder()
              .WithName(name)
              .Build();

        Assert.Equal(name, pipeline.Name);
    }

    [Fact]
    public void When_pipeline_name_not_set_defaults_to_null()
    {
        IPipeline pipeline = new PipelineBuilder()
              .Build();

        Assert.Null(pipeline.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void When_pipeline_name_set_to_empty_throws_exception(string name)
    {
        Assert.Throws<ArgumentException>(() => new PipelineBuilder().WithName(name));
    }

    [Fact]
    public void When_pipeline_name_set_to_null_throws_exception()
    {
        Assert.Throws<ArgumentNullException>(() => new PipelineBuilder().WithName(null!));
    }
}
