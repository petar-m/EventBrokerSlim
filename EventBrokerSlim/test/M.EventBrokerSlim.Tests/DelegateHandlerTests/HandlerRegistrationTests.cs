using FuncPipeline;
using Xunit.Abstractions;

namespace M.EventBrokerSlim.Tests.DelegateHandlerTests;

public  class HandlerRegistrationTests
{
    private readonly ServiceCollection _serviceCollection;

    public HandlerRegistrationTests(ITestOutputHelper output)
    {
        _serviceCollection = new ServiceCollection();
    }

    [Fact]
    public void PipelineRegisty_is_registered_in_service_provider()
    {
        _serviceCollection.AddEventBroker();
        var pipelineBuilder = PipelineBuilder.Create();
        pipelineBuilder.NewPipeline()
            .Execute(static async (Event1 event1, EventsTracker tracker, INext next) =>
            {
                tracker.Track(event1);
                await next.RunAsync();
            })
            .Execute(static async (Event1 event1, EventsTracker tracker) => await tracker.TrackAsync(event1))
            .Build(x => _serviceCollection.AddEventHandlerPipeline<Event1>(x));
        using var services = _serviceCollection.BuildServiceProvider(true);
        using var scope = services.CreateScope();

        var pipelineRegistry = scope.ServiceProvider.GetRequiredService<PipelineRegistry>();

        Assert.Equal(pipelineBuilder.Pipelines[0], pipelineRegistry.Get(typeof(Event1))[0]);
    }

    [Fact]
    public void Keyed_PipelineRegisty_is_registered_in_service_provider()
    {
        var key = "key";
        _serviceCollection.AddKeyedEventBroker("key");
        var pipelineBuilder = PipelineBuilder.Create();
        pipelineBuilder.NewPipeline()
            .Execute(static async (Event1 event1, EventsTracker tracker, INext next) =>
            {
                tracker.Track(event1);
                await next.RunAsync();
            })
            .Execute(static async (Event1 event1, EventsTracker tracker) => await tracker.TrackAsync(event1))
            .Build(x => _serviceCollection.AddEventHandlerPipeline<Event1>(x, key));
        using var services = _serviceCollection.BuildServiceProvider(true);
        using var scope = services.CreateScope();

        var pipelineRegistry = scope.ServiceProvider.GetRequiredKeyedService<PipelineRegistry>(key);
        Assert.Equal(pipelineBuilder.Pipelines[0], pipelineRegistry.Get(typeof(Event1))[0]);
        Assert.Null(scope.ServiceProvider.GetService<PipelineRegistry>());
    }
}
