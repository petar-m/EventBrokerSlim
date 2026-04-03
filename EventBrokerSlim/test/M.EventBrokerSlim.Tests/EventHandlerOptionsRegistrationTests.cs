using FuncPipeline;
using MELT;
using Microsoft.Extensions.Logging;

namespace M.EventBrokerSlim.Tests;

public class EventHandlerOptionsRegistrationTests
{
    private readonly ITestOutputHelper _output;
    private readonly EventsTracker _tracker;

    public EventHandlerOptionsRegistrationTests(ITestOutputHelper output)
    {
        _output = output;
        _tracker = new EventsTracker();
    }

    [Fact]
    public void AddTransientEventHandler_WithOptions_RegistersAsTransient()
    {
        var serviceCollection = new ServiceCollection()
            .AddEventBroker()
            .AddTransientEventHandler<TestEventBase, TestHandler1>(o => o
                .WithServiceKey("my-key"));

        var serviceDescriptor = serviceCollection.Single(x => x.IsKeyedService && x.KeyedImplementationType == typeof(TestHandler1));

        Assert.Equal(ServiceLifetime.Transient, serviceDescriptor.Lifetime);
    }

    [Fact]
    public void AddScopedEventHandler_WithOptions_RegistersAsScoped()
    {
        var serviceCollection = new ServiceCollection()
            .AddEventBroker()
            .AddScopedEventHandler<TestEventBase, TestHandler1>(o => o
                .WithServiceKey("my-key"));

        var serviceDescriptor = serviceCollection.Single(x => x.IsKeyedService && x.KeyedImplementationType == typeof(TestHandler1));

        Assert.Equal(ServiceLifetime.Scoped, serviceDescriptor.Lifetime);
    }

    [Fact]
    public void AddSingletonEventHandler_WithOptions_RegistersAsSingleton()
    {
        var serviceCollection = new ServiceCollection()
            .AddEventBroker()
            .AddSingletonEventHandler<TestEventBase, TestHandler1>(o => o
                .WithServiceKey("my-key"));

        var serviceDescriptor = serviceCollection.Single(x => x.IsKeyedService && x.KeyedImplementationType == typeof(TestHandler1));

        Assert.Equal(ServiceLifetime.Singleton, serviceDescriptor.Lifetime);
    }

    [Fact]
    public async Task AddTransientEventHandler_WithForBroker_RegistersWithKeyedBroker()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        serviceCollection
            .AddEventBroker()
            .AddKeyedEventBroker("broker1")
            .AddLogging(x => x.AddTest())
            .AddSingleton(_tracker)
            .AddTransientEventHandler<TestEventBase, TestHandler1>(o => o.ForBroker("broker1"));

        using var services = serviceCollection.BuildServiceProvider(true);
        using var scope = services.CreateScope();
        var eventBroker1 = scope.ServiceProvider.GetRequiredKeyedService<IEventBroker>("broker1");
        _tracker.ExpectedItemsCount = 1;

        // Act
        await eventBroker1.Publish(new TestEventBase(1));
        await _tracker.Wait(TimeSpan.FromSeconds(1));

        // Assert
        var items = _tracker.Items.Select(x => x.Item).OfType<TestEventBase>().ToArray();
        Assert.Single(items);
    }

    [Fact]
    public async Task AddScopedEventHandler_WithForBroker_RegistersWithKeyedBroker()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        serviceCollection
            .AddEventBroker()
            .AddKeyedEventBroker("broker1")
            .AddLogging(x => x.AddTest())
            .AddSingleton(_tracker)
            .AddScopedEventHandler<TestEventBase, TestHandler1>(o => o.ForBroker("broker1"));

        using var services = serviceCollection.BuildServiceProvider(true);
        using var scope = services.CreateScope();
        var eventBroker1 = scope.ServiceProvider.GetRequiredKeyedService<IEventBroker>("broker1");
        _tracker.ExpectedItemsCount = 1;

        // Act
        await eventBroker1.Publish(new TestEventBase(1));
        await _tracker.Wait(TimeSpan.FromSeconds(1));

        // Assert
        var items = _tracker.Items.Select(x => x.Item).OfType<TestEventBase>().ToArray();
        Assert.Single(items);
    }

    [Fact]
    public async Task AddSingletonEventHandler_WithForBroker_RegistersWithKeyedBroker()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        serviceCollection
            .AddEventBroker()
            .AddKeyedEventBroker("broker1")
            .AddLogging(x => x.AddTest())
            .AddSingleton(_tracker)
            .AddSingletonEventHandler<TestEventBase, TestHandler1>(o => o.ForBroker("broker1"));

        using var services = serviceCollection.BuildServiceProvider(true);
        using var scope = services.CreateScope();
        var eventBroker1 = scope.ServiceProvider.GetRequiredKeyedService<IEventBroker>("broker1");
        _tracker.ExpectedItemsCount = 1;

        // Act
        await eventBroker1.Publish(new TestEventBase(1));
        await _tracker.Wait(TimeSpan.FromSeconds(1));

        // Assert
        var items = _tracker.Items.Select(x => x.Item).OfType<TestEventBase>().ToArray();
        Assert.Single(items);
    }

    [Fact]
    public async Task AddEventHandlerPipeline_WithForBroker_RegistersWithKeyedBroker()
    {
        // Arrange
        var pipeline = PipelineBuilder.Create()
            .NewPipeline()
            .Execute(async static (TestEventBase testEvent, EventsTracker tracker) => await tracker.TrackAsync(testEvent))
            .Build()
            .Pipelines[0];

        var serviceCollection = new ServiceCollection();
        serviceCollection
            .AddEventBroker()
            .AddKeyedEventBroker("broker1")
            .AddLogging(x => x.AddTest())
            .AddSingleton(_tracker)
            .AddEventHandlerPipeline<TestEventBase>(pipeline, o => o.ForBroker("broker1"));

        using var services = serviceCollection.BuildServiceProvider(true);
        using var scope = services.CreateScope();
        var eventBroker1 = scope.ServiceProvider.GetRequiredKeyedService<IEventBroker>("broker1");
        _tracker.ExpectedItemsCount = 1;

        // Act
        await eventBroker1.Publish(new TestEventBase(1));
        await _tracker.Wait(TimeSpan.FromSeconds(1));

        // Assert
        var items = _tracker.Items.Select(x => x.Item).OfType<TestEventBase>().ToArray();
        Assert.Single(items);
    }

    [Fact]
    public async Task Keyed_Handler_Not_Used_By_Default_Broker()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        serviceCollection
            .AddEventBroker()
            .AddKeyedEventBroker("broker1")
            .AddLogging(x => x.AddTest())
            .AddSingleton(_tracker)
            .AddTransientEventHandler<TestEventBase, TestHandler1>(o => o.ForBroker("broker1"));

        using var services = serviceCollection.BuildServiceProvider(true);
        using var scope = services.CreateScope();
        var defaultBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        _tracker.ExpectedItemsCount = 1;

        // Act
        await defaultBroker.Publish(new TestEventBase(1));
        await _tracker.Wait(TimeSpan.FromSeconds(1));

        // Assert
        var items = _tracker.Items.Select(x => x.Item).OfType<TestEventBase>().ToArray();
        Assert.Empty(items);

        var provider = (TestLoggerProvider)scope.ServiceProvider.GetServices<ILoggerProvider>().Single(x => x is TestLoggerProvider);
        LogEntry log = Assert.Single(provider.Sink.LogEntries);
        Assert.Equal("No event handler found for event M.EventBrokerSlim.Tests.TestEventBase", log.Message);
    }

    [Fact]
    public void AddTransientEventHandler_WithHandlerName_SetsHandlerName()
    {
        // Arrange
        var serviceCollection = new ServiceCollection()
            .AddEventBroker()
            .AddTransientEventHandler<TestEventBase, TestHandler1>(o => o.WithHandlerName("my-handler"));

        using var services = serviceCollection.BuildServiceProvider(true);
        using var scope = services.CreateScope();
        var pipelineRegistry = scope.ServiceProvider.GetRequiredService<PipelineRegistry>();

        // Assert
        var pipelines = pipelineRegistry.Get(typeof(TestEventBase));
        Assert.Single(pipelines);
        Assert.Equal("my-handler", pipelines[0].HandlerName);
    }

    [Fact]
    public void AddEventHandlerPipeline_WithHandlerName_SetsHandlerName()
    {
        // Arrange
        var pipeline = PipelineBuilder.Create()
            .NewPipeline()
            .Execute(async static (TestEventBase testEvent, EventsTracker tracker) => await tracker.TrackAsync(testEvent))
            .Build()
            .Pipelines[0];

        var serviceCollection = new ServiceCollection()
            .AddEventBroker()
            .AddSingleton(_tracker)
            .AddEventHandlerPipeline<TestEventBase>(pipeline, o => o.WithHandlerName("pipeline-handler"));

        using var services = serviceCollection.BuildServiceProvider(true);
        using var scope = services.CreateScope();
        var pipelineRegistry = scope.ServiceProvider.GetRequiredService<PipelineRegistry>();

        // Assert
        var pipelines = pipelineRegistry.Get(typeof(TestEventBase));
        Assert.Single(pipelines);
        Assert.Equal("pipeline-handler", pipelines[0].HandlerName);
    }

    [Fact]
    public async Task AllOptions_Combined_WorkTogether()
    {
        // Arrange
        var pipeline = PipelineBuilder.Create()
            .NewPipeline()
            .Execute(async static (TestEventBase testEvent, EventsTracker tracker) => await tracker.TrackAsync(testEvent))
            .Build()
            .Pipelines[0];

        var serviceCollection = new ServiceCollection();
        serviceCollection
            .AddEventBroker()
            .AddKeyedEventBroker("broker1")
            .AddLogging(x => x.AddTest())
            .AddSingleton(_tracker)
            .AddEventHandlerPipeline<TestEventBase>(pipeline, o => o
                .ForBroker("broker1")
                .WithHandlerName("combined-handler"))
            .AddTransientEventHandler<TestEventBase, TestHandler1>(o => o
                .ForBroker("broker1")
                .WithServiceKey("custom-key"))
            .AddScopedEventHandler<TestEventBase, TestHandler2>(o => o
                .ForBroker("broker1"))
            .AddSingletonEventHandler<TestEventBase, TestHandler3>(o => o
                .ForBroker("broker1"));

        using var services = serviceCollection.BuildServiceProvider(true);
        using var scope = services.CreateScope();
        var eventBroker1 = scope.ServiceProvider.GetRequiredKeyedService<IEventBroker>("broker1");
        _tracker.ExpectedItemsCount = 4;

        // Act
        await eventBroker1.Publish(new TestEventBase(1));
        await _tracker.Wait(TimeSpan.FromSeconds(1));

        // Assert
        var items = _tracker.Items.Select(x => x.Item).OfType<TestEventBase>().ToArray();
        Assert.Equal(4, items.Length);

        // Verify the pipeline handler name was set
        var pipelineRegistry = scope.ServiceProvider.GetRequiredKeyedService<PipelineRegistry>("broker1");
        var pipelines = pipelineRegistry.Get(typeof(TestEventBase));
        Assert.Contains(pipelines, p => p.HandlerName == "combined-handler");
    }

    public class TestHandler1 : IEventHandler<TestEventBase>
    {
        private readonly EventsTracker _tracker;
        public TestHandler1(EventsTracker tracker) => _tracker = tracker;
        public Task Handle(TestEventBase @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken) => _tracker.TrackAsync(@event);
        public Task OnError(Exception exception, TestEventBase @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    public class TestHandler2 : IEventHandler<TestEventBase>
    {
        private readonly EventsTracker _tracker;
        public TestHandler2(EventsTracker tracker) => _tracker = tracker;
        public Task Handle(TestEventBase @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken) => _tracker.TrackAsync(@event);
        public Task OnError(Exception exception, TestEventBase @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    public class TestHandler3 : IEventHandler<TestEventBase>
    {
        private readonly EventsTracker _tracker;
        public TestHandler3(EventsTracker tracker) => _tracker = tracker;
        public Task Handle(TestEventBase @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken) => _tracker.TrackAsync(@event);
        public Task OnError(Exception exception, TestEventBase @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
