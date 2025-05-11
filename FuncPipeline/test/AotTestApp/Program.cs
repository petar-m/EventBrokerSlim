using FuncPipeline;
using Microsoft.Extensions.DependencyInjection;

namespace AotTestApp;

internal class Program
{
    static async Task Main(string[] args)
    {
        using var services = new ServiceCollection()
            .AddSingleton<DateService>()
            .BuildServiceProvider(true);

        var pipeline = PipelineBuilder.Create(services.GetRequiredService<IServiceScopeFactory>())
            .NewPipeline()
            .Execute<string, INext>(async ([ResolveFrom(PrimarySource = Source.Context, Fallback =false, PrimaryNotFound = NotFoundBehavior.ThrowException)]message, next) =>
            {
                Console.WriteLine($"Message: {message}");
                await next.RunAsync();
                Console.WriteLine($"Completed pipeline.");
            })
            .Execute<DateService>(date =>
            {
                Console.WriteLine($"Date: {date.GetTime().Result}");
                return Task.CompletedTask;
            })
            .Build()
            .Pipelines[0];

        var context = new PipelineRunContext().Set<string>("resolved from context");
        var result = await pipeline.RunAsync(context);
        Console.WriteLine($"Pipeline result: IsSuccessful = {result.IsSuccessful}");

        result = await pipeline.RunAsync();
        Console.WriteLine($"Pipeline result: IsSuccessful = {result.IsSuccessful}, Exception = {result.Exception}");
    }
}

public class DateService
{
     public Task<string> GetTime() => Task.FromResult(DateTime.Now.ToLongDateString());
}
