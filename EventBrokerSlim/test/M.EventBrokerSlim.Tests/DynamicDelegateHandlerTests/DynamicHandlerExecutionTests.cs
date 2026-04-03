using FuncPipeline;
using Microsoft.Extensions.Logging;

namespace M.EventBrokerSlim.Tests.DynamicDelegateHandlerTests;

public class DynamicHandlerExecutionTests
{
    private readonly ITestOutputHelper _output;
    private readonly ServiceCollection _serviceCollection;
    private readonly EventsTracker _tracker;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public DynamicHandlerExecutionTests(ITestOutputHelper output)
    {
        _output = output;
        _tracker = new EventsTracker();
        _serviceCollection = new ServiceCollection();
        _serviceCollection
            .AddEventBroker()
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
        var dynamicEventHandlers = scope.ServiceProvider.GetRequiredService<IDynamicEventHandlers>();
        _tracker.ExpectedItemsCount = 1;

        // Act
        await eventBroker.Publish(new TestEventBase(1), _ct);
        await Task.Delay(TimeSpan.FromMilliseconds(50), _ct);

        _ = dynamicEventHandlers.Add<TestEventBase>(pipeline);
        await eventBroker.Publish(new TestEventBase(2), _ct);
        await _tracker.Wait(TimeSpan.FromSeconds(300));

        // Assert
        var items = _tracker.Items.Select(x => x.Item).OfType<TestEventBase>().ToArray();
        Assert.Single(items);
        Assert.Equal(2, items[0].Number);

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
        var dynamicEventHandlers = scope.ServiceProvider.GetRequiredService<IDynamicEventHandlers>();
        _tracker.ExpectedItemsCount = 1;

        // Act
        await eventBroker.Publish(new TestEventBase(1), _ct);
        await Task.Delay(TimeSpan.FromMilliseconds(50), _ct);

        IDynamicHandlerClaimTicket claimTicket = dynamicEventHandlers.Add<TestEventBase>(pipeline);
        await eventBroker.Publish(new TestEventBase(2), _ct);
        await Task.Delay(TimeSpan.FromMilliseconds(50), _ct);

        dynamicEventHandlers.Remove(claimTicket);

        await eventBroker.Publish(new TestEventBase(3), _ct);
        await _tracker.Wait(TimeSpan.FromMilliseconds(300));

        // Assert
        var items = _tracker.Items.Select(x => x.Item).OfType<TestEventBase>().ToArray();
        Assert.Single(items);
        Assert.Equal(2, items[0].Number);

        _output.WriteLine($"Elapsed: {_tracker.Elapsed}");
    }

    [Fact]
    public async Task Multiple_Dynamic_Handlers_Added()
    {
        // Arrange
        var builder = PipelineBuilder.Create()
            .NewPipeline()
            .Execute(async static (TestEventBase testEvent, EventsTracker tracker) => await tracker.TrackAsync(testEvent))
            .Build()
            .NewPipeline()
            .Execute(async static (TestEventBase testEvent, EventsTracker tracker) => await tracker.TrackAsync(testEvent))
            .Build();

        using var services = _serviceCollection.BuildServiceProvider(true);
        using var scope = services.CreateScope();
        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var dynamicEventHandlers = scope.ServiceProvider.GetRequiredService<IDynamicEventHandlers>();
        _tracker.ExpectedItemsCount = 2;

        // Act
        await eventBroker.Publish(new TestEventBase(1), _ct);
        await Task.Delay(TimeSpan.FromMilliseconds(50), _ct);

        _ = dynamicEventHandlers.Add<TestEventBase>(builder.Pipelines[0]);
        _ = dynamicEventHandlers.Add<TestEventBase>(builder.Pipelines[1]);
        await eventBroker.Publish(new TestEventBase(2), _ct);

        await _tracker.Wait(TimeSpan.FromMilliseconds(300));

        // Assert
        var items = _tracker.Items.Select(x => x.Item).OfType<TestEventBase>().ToArray();
        Assert.Equal(2, items.Length);
        Assert.Equal(2, items[0].Number);
        Assert.Equal(2, items[1].Number);

        _output.WriteLine($"Elapsed: {_tracker.Elapsed}");
    }

    [Fact]
    public async Task Multiple_Dynamic_Handlers_Removed()
    {
        // Arrange
        var builder = PipelineBuilder.Create()
            .NewPipeline()
            .Execute(async static (TestEventBase testEvent, EventsTracker tracker) => await tracker.TrackAsync(testEvent))
            .Build()
            .NewPipeline()
            .Execute(async static (TestEventBase testEvent, EventsTracker tracker) => await tracker.TrackAsync(testEvent))
            .Build();

        using var services = _serviceCollection.BuildServiceProvider(true);
        using var scope = services.CreateScope();
        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var dynamicEventHandlers = scope.ServiceProvider.GetRequiredService<IDynamicEventHandlers>();
        _tracker.ExpectedItemsCount = 2;

        // Act
        await eventBroker.Publish(new TestEventBase(1), _ct);
        await Task.Delay(TimeSpan.FromMilliseconds(50), _ct);

        IDynamicHandlerClaimTicket claimTicket1 = dynamicEventHandlers.Add<TestEventBase>(builder.Pipelines[0]);
        IDynamicHandlerClaimTicket claimTicket2 = dynamicEventHandlers.Add<TestEventBase>(builder.Pipelines[1]);

        await eventBroker.Publish(new TestEventBase(2), _ct);
        await Task.Delay(TimeSpan.FromMilliseconds(50), _ct);

        dynamicEventHandlers.RemoveRange([claimTicket1, claimTicket2]);

        await eventBroker.Publish(new TestEventBase(3), _ct);

        await _tracker.Wait(TimeSpan.FromMilliseconds(300));

        // Assert
        var items = _tracker.Items.Select(x => x.Item).OfType<TestEventBase>().ToArray();
        Assert.Equal(2, items.Length);
        Assert.Equal(2, items[0].Number);
        Assert.Equal(2, items[1].Number);

        _output.WriteLine($"Elapsed: {_tracker.Elapsed}");
    }
}
