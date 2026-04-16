using FuncPipeline;
using M.EventBrokerSlim.Persistent;

namespace M.EventBrokerSlim.Tests.DelegateHandlerTests;

public  class HandlerRegistrationTests
{
    private readonly ServiceCollection _serviceCollection;

    public HandlerRegistrationTests(ITestOutputHelper output)
    {
        _serviceCollection = new ServiceCollection();
    }

    [Fact]
    public void PipelineRegistry_is_registered_in_service_provider()
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

        Assert.Equal(pipelineBuilder.Pipelines[0], pipelineRegistry.Get(typeof(Event1))[0].Pipeline);
    }

    [Fact]
    public void Keyed_PipelineRegistry_is_registered_in_service_provider()
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
        Assert.Equal(pipelineBuilder.Pipelines[0], pipelineRegistry.Get(typeof(Event1))[0].Pipeline);
        Assert.Null(scope.ServiceProvider.GetService<PipelineRegistry>());
    }

    [Fact]
    public void NullPipeline_is_not_registered_as_handler()
    {
        _serviceCollection.AddEventBroker();
        _serviceCollection.AddEventHandlerPipeline<Event1>(NullPipeline.Instance);
        using var services = _serviceCollection.BuildServiceProvider(true);
        using var scope = services.CreateScope();

        var pipelineRegistry = scope.ServiceProvider.GetRequiredService<PipelineRegistry>();

        Assert.Empty(pipelineRegistry.Get(typeof(Event1)));
    }

    [Fact]
    public void NullPipeline_is_not_registered_as_named_handler()
    {
        _serviceCollection.AddEventBroker();
        _serviceCollection.AddEventHandlerPipeline<Event1>(NullPipeline.Instance, handlerName: "null-handler");
        using var services = _serviceCollection.BuildServiceProvider(true);
        using var scope = services.CreateScope();

        var pipelineRegistry = scope.ServiceProvider.GetRequiredService<PipelineRegistry>();

        Assert.Null(pipelineRegistry.Get("null-handler"));
    }

    [Fact]
    public void NullPipeline_is_not_registered_as_handler_name()
    {
        _serviceCollection.AddEventBroker();
        _serviceCollection.AddEventHandlerPipeline<Event1>(NullPipeline.Instance, handlerName: "null-handler");
        using var services = _serviceCollection.BuildServiceProvider(true);
        using var scope = services.CreateScope();

        var pipelineRegistry = scope.ServiceProvider.GetRequiredService<PipelineRegistry>();

        var name = Assert.Single(pipelineRegistry.GetHandlerNames<Event1>());
        Assert.Equal("null-handler", name);
    }
}
