using FuncPipeline;
using MELT;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace M.EventBrokerSlim.Tests.DynamicDelegateHandlerTests;

public class KeyedHandlerExecutionTests
{
    private readonly ITestOutputHelper _output;
    private readonly ServiceCollection _serviceCollection;
    private readonly EventsTracker _tracker;

    public KeyedHandlerExecutionTests(ITestOutputHelper output)
    {
        _output = output;
        _tracker = new EventsTracker();
        _serviceCollection = new ServiceCollection();
        _serviceCollection
            .AddEventBroker()
            .AddKeyedEventBroker("broker1")
            .AddLogging(x => x.AddTest())
            .AddSingleton(_tracker);
    }

    [Fact]
    public async Task Keyed_Handler_Registrations_Used_By_Keyed_EventBroker()
    {
        // Arrange
        var pipeline = PipelineBuilder.Create()
            .NewPipeline()
            .Execute(async static (TestEventBase testEvent, EventsTracker tracker) => await tracker.TrackAsync(testEvent))
            .Build()
            .Pipelines[0];

        using var services = _serviceCollection
            .AddEventHandlerPipeline<TestEventBase>(pipeline, "broker1")
            .AddTransientEventHandler<TestEventBase, TestEventHandler1>(eventBrokerKey: "broker1")
            .AddScopedEventHandler<TestEventBase, TestEventHandler2>(eventBrokerKey: "broker1")
            .AddSingletonEventHandler<TestEventBase, TestEventHandler3>(eventBrokerKey: "broker1")
            .BuildServiceProvider(true);
        using var scope = services.CreateScope();
        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventBroker1 = scope.ServiceProvider.GetRequiredKeyedService<IEventBroker>("broker1");
        var dynamicEventHandlers1 = scope.ServiceProvider.GetRequiredKeyedService<IDynamicEventHandlers>("broker1");
        _tracker.ExpectedItemsCount = 4;

        // Act
        await eventBroker1.Publish(new TestEventBase(1));

        await _tracker.Wait(TimeSpan.FromSeconds(1));

        // Assert
        var items = _tracker.Items.Select(x => x.Item).OfType<TestEventBase>().ToArray();
        Assert.Equal(4, items.Length);

        var provider = (TestLoggerProvider)scope.ServiceProvider.GetServices<ILoggerProvider>().Single(x => x is TestLoggerProvider);
        // No ""No event handler found for event..." logged.
        Assert.Empty(provider.Sink.LogEntries);

        _output.WriteLine($"Elapsed: {_tracker.Elapsed}");
    }

    [Fact]
    public async Task Keyed_Handler_Registrations_Not_Used_By_Default_EventBroker()
    {
        // Arrange
        var pipeline = PipelineBuilder.Create()
            .NewPipeline()
            .Execute(async static (TestEventBase testEvent, EventsTracker tracker) => await tracker.TrackAsync(testEvent))
            .Build()
            .Pipelines[0];

        using var services = _serviceCollection
            .AddEventHandlerPipeline<TestEventBase>(pipeline, "broker1")
            .AddTransientEventHandler<TestEventBase, TestEventHandler1>(eventBrokerKey: "broker1")
            .AddScopedEventHandler<TestEventBase, TestEventHandler2>(eventBrokerKey: "broker1")
            .AddSingletonEventHandler<TestEventBase, TestEventHandler3>(eventBrokerKey: "broker1")
            .BuildServiceProvider(true);
        using var scope = services.CreateScope();
        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventBroker1 = scope.ServiceProvider.GetRequiredKeyedService<IEventBroker>("broker1");
        var dynamicEventHandlers1 = scope.ServiceProvider.GetRequiredKeyedService<IDynamicEventHandlers>("broker1");
        _tracker.ExpectedItemsCount = 4;

        // Act
        await eventBroker.Publish(new TestEventBase(1));

        await _tracker.Wait(TimeSpan.FromSeconds(1));

        // Assert
        var items = _tracker.Items.Select(x => x.Item).OfType<TestEventBase>().ToArray();
        Assert.Empty(items);

        var provider = (TestLoggerProvider)scope.ServiceProvider.GetServices<ILoggerProvider>().Single(x => x is TestLoggerProvider);
        LogEntry log = Assert.Single(provider.Sink.LogEntries);
        Assert.Equal("No event handler found for event M.EventBrokerSlim.Tests.TestEventBase", log.Message);

        _output.WriteLine($"Elapsed: {_tracker.Elapsed}");
    }

    [Fact]
    public async Task Keyed_Handler_Registrations_Not_Used_By_Another_Keyed_EventBroker()
    {
        // Arrange
        var pipeline = PipelineBuilder.Create()
            .NewPipeline()
            .Execute(async static (TestEventBase testEvent, EventsTracker tracker) => await tracker.TrackAsync(testEvent))
            .Build()
            .Pipelines[0];

        using var services = _serviceCollection
            .AddEventHandlerPipeline<TestEventBase>(pipeline, "broker2")
            .AddTransientEventHandler<TestEventBase, TestEventHandler1>(eventBrokerKey: "broker2")
            .AddScopedEventHandler<TestEventBase, TestEventHandler2>(eventBrokerKey: "broker2")
            .AddSingletonEventHandler<TestEventBase, TestEventHandler3>(eventBrokerKey: "broker2")
            .BuildServiceProvider(true);
        using var scope = services.CreateScope();
        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventBroker1 = scope.ServiceProvider.GetRequiredKeyedService<IEventBroker>("broker1");
        var dynamicEventHandlers1 = scope.ServiceProvider.GetRequiredKeyedService<IDynamicEventHandlers>("broker1");
        _tracker.ExpectedItemsCount = 4;

        // Act
        await eventBroker.Publish(new TestEventBase(1));
        await eventBroker1.Publish(new TestEventBase(1));

        await _tracker.Wait(TimeSpan.FromSeconds(1));

        // Assert
        var items = _tracker.Items.Select(x => x.Item).OfType<TestEventBase>().ToArray();
        Assert.Empty(items);

        var provider = (TestLoggerProvider)scope.ServiceProvider.GetServices<ILoggerProvider>().Single(x => x is TestLoggerProvider);
        Assert.Equal(2, provider.Sink.LogEntries.Count());
        Assert.All(provider.Sink.LogEntries, x => Assert.Equal("No event handler found for event M.EventBrokerSlim.Tests.TestEventBase", x.Message));

        _output.WriteLine($"Elapsed: {_tracker.Elapsed}");
    }

    public class TestEventHandler1 : IEventHandler<TestEventBase>
    {
        private readonly EventsTracker _tracker;

        public TestEventHandler1(EventsTracker tracker) => _tracker = tracker;

        public Task Handle(TestEventBase @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken) => _tracker.TrackAsync(@event);

        public Task OnError(Exception exception, TestEventBase @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    public class TestEventHandler2 : IEventHandler<TestEventBase>
    {
        private readonly EventsTracker _tracker;

        public TestEventHandler2(EventsTracker tracker) => _tracker = tracker;

        public Task Handle(TestEventBase @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken) => _tracker.TrackAsync(@event);

        public Task OnError(Exception exception, TestEventBase @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    public class TestEventHandler3 : IEventHandler<TestEventBase>
    {
        private readonly EventsTracker _tracker;

        public TestEventHandler3(EventsTracker tracker) => _tracker = tracker;

        public Task Handle(TestEventBase @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken) => _tracker.TrackAsync(@event);

        public Task OnError(Exception exception, TestEventBase @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
