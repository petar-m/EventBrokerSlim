using FuncPipeline;
using M.EventBrokerSlim;
using M.EventBrokerSlim.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace AotTestApp;

internal static class Program
{
    static async Task Main(string[] args)
    {
        var pipeline = PipelineBuilder.Create()
            .NewPipeline()
            .Execute(async (DateService dateService, string message) =>
            {
                Console.WriteLine($"Pipeline: {message} {await dateService.GetTime()}");
            })
            .Build()
            .Pipelines[0];

        using var services = new ServiceCollection()
            .AddEventBroker()
            .AddEventHandlerPipeline<string>(pipeline)
            .AddScopedEventHandler<string, Handler>()
            .AddSingleton<DateService>()
            .BuildServiceProvider(true);

        using var scope = services.CreateScope();
        var broker = scope.ServiceProvider.GetRequiredService<IEventBroker>();

        await broker.Publish("Hello world");
        Console.Read();
    }

    public class DateService
    {
        public Task<string> GetTime() => Task.FromResult(DateTime.Now.ToLongDateString());
    }

    public class Handler : IEventHandler<string>
    {
        private readonly DateService _dateService;

        public Handler(DateService d)
        {
            _dateService = d;
        }

        public Task Handle(string @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Handler: {@event} {_dateService.GetTime().Result}");
            return Task.CompletedTask;
        }

        public Task OnError(Exception exception, string @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
