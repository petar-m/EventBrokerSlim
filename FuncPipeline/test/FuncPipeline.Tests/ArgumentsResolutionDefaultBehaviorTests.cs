using System;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FuncPipeline.Tests;

public class ArgumentsResolutionDefaultBehaviorTests
{
    [Fact]
    public async Task No_ServiceProvider_NoContext_ReferenceType_Parameter_Is_Null()
    {
        CancellationToken cancellationToken = default;
        ITestStub referenceType = A.Fake<ITestStub>(x => x.Strict());

        IPipeline pipeline = PipelineBuilder.Create()
            .NewPipeline()
              .Execute((ITestStub x, CancellationToken ct) =>
              {
                  referenceType = x;
                  return Task.CompletedTask;
              })
              .Build()
              .Pipelines[0];

        PipelineRunResult result = await pipeline.RunAsync(cancellationToken: cancellationToken);

        Assert.True(result.IsSuccessful);
        Assert.Null(referenceType);
    }

    [Fact]
    public async Task No_ServiceProvider_NoContext_ValueType_Parameter_Is_Default()
    {
        CancellationToken cancellationToken = default;
        (string, int) valueType = ("abc", 123);

        IPipeline pipeline = PipelineBuilder.Create()
            .NewPipeline()
              .Execute(((string, int) x, CancellationToken ct) =>
              {
                  valueType = x;
                  return Task.CompletedTask;
              })
              .Build()
              .Pipelines[0];

        PipelineRunResult result = await pipeline.RunAsync(cancellationToken: cancellationToken);

        Assert.True(result.IsSuccessful);
        Assert.Equal(default, valueType);
    }

    [Fact]
    public async Task Parameter_Resolved_From_Service_Provider()
    {
        CancellationToken cancellationToken = default;

        ITestStub serviceProviderFunc = A.Fake<ITestStub>(x => x.Strict());
        A.CallTo(() => serviceProviderFunc.Execute<string>())
            .Returns("from service provider");
        A.CallTo(() => serviceProviderFunc.ExecuteAsync("from service provider", default))
            .Returns(Task.CompletedTask);

        var serviceProvider = new ServiceCollection()
            .AddSingleton<ITestStub>(serviceProviderFunc)
            .BuildServiceProvider();

        IPipeline pipeline = PipelineBuilder.Create(serviceProvider.GetRequiredService<IServiceScopeFactory>())
            .NewPipeline()
              .Execute(static async(ITestStub x, CancellationToken ct) =>
              {
                  var message = x.Execute<string>();
                  await x.ExecuteAsync(message, ct);
              })
              .Build()
              .Pipelines[0];

        PipelineRunResult result = await pipeline.RunAsync(cancellationToken: cancellationToken);

        Assert.True(result.IsSuccessful);
        A.CallTo(() => serviceProviderFunc.Execute<string>())
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => serviceProviderFunc.ExecuteAsync("from service provider", default))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Parameter_Resolved_From_Context_If_Not_In_Service_Provider()
    {
        CancellationToken cancellationToken = default;

        ITestStub contextFunc = A.Fake<ITestStub>(x => x.Strict());
        A.CallTo(() => contextFunc.Execute<string>())
            .Returns("from context");
        A.CallTo(() => contextFunc.ExecuteAsync("from context", default))
            .Returns(Task.CompletedTask);

        var context = new PipelineRunContext().Set(typeof(ITestStub), contextFunc);

        var serviceProvider = new ServiceCollection()
            .BuildServiceProvider();

        IPipeline pipeline = PipelineBuilder.Create(serviceProvider.GetRequiredService<IServiceScopeFactory>())
            .NewPipeline()
              .Execute(static async(ITestStub x, CancellationToken ct) =>
              {
                  var message = x.Execute<string>();
                  await x.ExecuteAsync(message, ct);
              })
              .Build()
              .Pipelines[0];

        PipelineRunResult result = await pipeline.RunAsync(context, cancellationToken);

        Assert.True(result.IsSuccessful);
        A.CallTo(() => contextFunc.Execute<string>())
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => contextFunc.ExecuteAsync("from context", default))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Context_Is_Available_As_Parameter()
    {
        CancellationToken cancellationToken = default;

        ITestStub contextFunc = A.Fake<ITestStub>(x => x.Strict());
        A.CallTo(() => contextFunc.Execute<string>())
            .Returns("from context");
        A.CallTo(() => contextFunc.ExecuteAsync("from context", default))
            .Returns(Task.CompletedTask);

        var context = new PipelineRunContext().Set(typeof(ITestStub), contextFunc);

        var serviceProvider = new ServiceCollection()
            .BuildServiceProvider();

        IPipeline pipeline = PipelineBuilder.Create(serviceProvider.GetRequiredService<IServiceScopeFactory>())
            .NewPipeline()
              .Execute(static async(PipelineRunContext c, CancellationToken ct) =>
              {
                  if(!c.TryGet<ITestStub>(out var x))
                  {
                      throw new ArgumentNullException();
                  }

                  var message = x!.Execute<string>();
                  await x.ExecuteAsync(message, ct);
              })
              .Build()
              .Pipelines[0];

        PipelineRunResult result = await pipeline.RunAsync(context, cancellationToken);

        Assert.True(result.IsSuccessful);
        A.CallTo(() => contextFunc.Execute<string>())
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => contextFunc.ExecuteAsync("from context", default))
            .MustHaveHappenedOnceExactly();
    }
}
