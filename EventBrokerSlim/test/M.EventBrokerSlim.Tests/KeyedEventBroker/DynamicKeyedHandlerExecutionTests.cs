using FuncPipeline;
using MELT;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace M.EventBrokerSlim.Tests.DynamicDelegateHandlerTests;

public class DynamicKeyedHandlerExecutionTests
{
    private readonly ITestOutputHelper _output;
    private readonly ServiceCollection _serviceCollection;
    private readonly EventsTracker _tracker;

    public DynamicKeyedHandlerExecutionTests(ITestOutputHelper output)
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
    public async Task Handler_Dynamically_Added()
    {
        // Arrange
        var pipeline = PipelineBuilder.Create()
            .NewPipeline()
            .Execute(async static (TestEventBase testEvent, EventsTracker tracker) => await tracker.TrackAsync(testEvent))
            .Build()
            .Pipelines[0];

        using var services = _serviceCollection.BuildServiceProvider(true);
        using var scope = services.CreateScope();
        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventBroker1 = scope.ServiceProvider.GetRequiredKeyedService<IEventBroker>("broker1");
        var dynamicEventHandlers1 = scope.ServiceProvider.GetRequiredKeyedService<IDynamicEventHandlers>("broker1");
        _tracker.ExpectedItemsCount = 1;

        // Act
        _ = dynamicEventHandlers1.Add<TestEventBase>(pipeline);
        await eventBroker1.Publish(new TestEventBase(2));

        await _tracker.Wait(TimeSpan.FromSeconds(1));

        // Assert
        var items = _tracker.Items.Select(x => x.Item).OfType<TestEventBase>().ToArray();
        Assert.Single(items);
        Assert.Equal(2, items[0].Number);

        var provider = (TestLoggerProvider)scope.ServiceProvider.GetServices<ILoggerProvider>().Single(x => x is TestLoggerProvider);
        // No ""No event handler found for event..." logged.
        Assert.Empty(provider.Sink.LogEntries);

        _output.WriteLine($"Elapsed: {_tracker.Elapsed}");
    }

    [Fact]
    public async Task Handler_Dynamically_Removed()
    {
        // Arrange
        var pipeline = PipelineBuilder.Create()
            .NewPipeline()
            .Execute(async static (TestEventBase testEvent, EventsTracker tracker) => await tracker.TrackAsync(testEvent))
            .Build()
            .Pipelines[0];

        using var services = _serviceCollection.BuildServiceProvider(true);
        using IServiceScope scope = services.CreateScope();
        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventBroker1 = scope.ServiceProvider.GetRequiredKeyedService<IEventBroker>("broker1");
        var dynamicEventHandlers = scope.ServiceProvider.GetRequiredKeyedService<IDynamicEventHandlers>("broker1");
        _tracker.ExpectedItemsCount = 1;

        // Act
        IDynamicHandlerClaimTicket claimTicket = dynamicEventHandlers.Add<TestEventBase>(pipeline);
        await eventBroker1.Publish(new TestEventBase(2));
        await Task.Delay(TimeSpan.FromMilliseconds(50));

        dynamicEventHandlers.Remove(claimTicket);

        await eventBroker1.Publish(new TestEventBase(3));

        await _tracker.Wait(TimeSpan.FromMilliseconds(300));

        // Assert
        var items = _tracker.Items.Select(x => x.Item).OfType<TestEventBase>().ToArray();
        Assert.Single(items);
        Assert.Equal(2, items[0].Number);

        _output.WriteLine($"Elapsed: {_tracker.Elapsed}");
    }
}
