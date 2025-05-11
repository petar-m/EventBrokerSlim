using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using FuncPipeline;
using Microsoft.Extensions.DependencyInjection;

namespace FuncPipelineBenchmarks;

internal static class Program
{
    static void Main(string[] args)
    {
        BenchmarkRunner.Run<Benchmarks>();
    }
}

[MemoryDiagnoser]
public class Benchmarks
{
    private ServiceProvider? _services;
    private IPipeline? _pipeline;

    [GlobalSetup]
    public void Setup()
    {
        _services = new ServiceCollection()
            .AddScoped<TestService>()
            .BuildServiceProvider(true);

        _pipeline = PipelineBuilder.Create(_services.GetRequiredService<IServiceScopeFactory>())
            .NewPipeline()
            .Execute<TestService, PipelineRunContext>((service, context) =>
            {
                var value = service.GetValue();
                context.Set<int>(value);
                return Task.CompletedTask;
            })
            .Build()
            .Pipelines[0];
    }

    [Benchmark]
    public async Task<int> FuncPipeline()
    {
        var result = await _pipeline!.RunAsync();
        if(!result.IsSuccessful) throw result.Exception!;
        _ = result.Context.TryGet<int>(out var value);
        return value;
    }

    [Benchmark]
    public int DirectCall()
    {
        using var scope = _services!.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<TestService>();
        return service.GetValue();
    }
}

public class TestService
{
    private readonly int _x;
    private readonly int _y;

    public TestService()
    {
        _x = Random.Shared.Next();
        _y = Random.Shared.Next();
    }

    public int GetValue()
    {
        return _x + _y;
    }
}
