using Xunit.Abstractions;

namespace M.EventBrokerSlim.Tests.DynamicDelegateHandlerTests;

public class DynamicHandlerExecutionTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ServiceProvider _serviceProvider;
    private readonly EventsTracker _tracker;

    public DynamicHandlerExecutionTests(ITestOutputHelper output)
    {
        _output = output;
        _tracker = new EventsTracker();
        _serviceProvider = ServiceProviderHelper.Build(
            x => x.AddEventBroker()
                  .AddSingleton(_tracker));
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }

    [Fact]
    public async Task Handler_Dynamically_Added()
    {
        // Arrange
        var handlerRegistryBuilder = new DelegateHandlerRegistryBuilder();
        handlerRegistryBuilder
            .RegisterHandler<TestEventBase>(async static (TestEventBase testEvent, EventsTracker tracker) => await tracker.TrackAsync(testEvent));

        using var scope = _serviceProvider.CreateScope();
        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var dynamicEventHandlers = scope.ServiceProvider.GetRequiredService<IDynamicEventHandlers>();
        _tracker.ExpectedItemsCount = 1;

        // Act
        await eventBroker.Publish(new TestEventBase(1));
        await Task.Delay(TimeSpan.FromMilliseconds(50));

        _ = dynamicEventHandlers.Add(handlerRegistryBuilder);
        await eventBroker.Publish(new TestEventBase(2));

        await _tracker.Wait(TimeSpan.FromMilliseconds(300));

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
        var handlerRegistryBuilder = new DelegateHandlerRegistryBuilder();
        handlerRegistryBuilder
            .RegisterHandler<TestEventBase>(async static (TestEventBase testEvent, EventsTracker tracker) => await tracker.TrackAsync(testEvent));

        using IServiceScope scope = _serviceProvider.CreateScope();
        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var dynamicEventHandlers = scope.ServiceProvider.GetRequiredService<IDynamicEventHandlers>();
        _tracker.ExpectedItemsCount = 1;

        // Act
        await eventBroker.Publish(new TestEventBase(1));
        await Task.Delay(TimeSpan.FromMilliseconds(50));

        IDynamicHandlerClaimTicket claimTicket = dynamicEventHandlers.Add(handlerRegistryBuilder);
        await eventBroker.Publish(new TestEventBase(2));
        await Task.Delay(TimeSpan.FromMilliseconds(50));

        dynamicEventHandlers.Remove(claimTicket);

        await eventBroker.Publish(new TestEventBase(3));

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
        var handlerRegistryBuilder = new DelegateHandlerRegistryBuilder();
        handlerRegistryBuilder
            .RegisterHandler<TestEventBase>(async static (TestEventBase testEvent, EventsTracker tracker) => await tracker.TrackAsync(testEvent))
            .Builder()
            .RegisterHandler<TestEventBase>(async static (TestEventBase testEvent, EventsTracker tracker) => await tracker.TrackAsync(testEvent));

        using var scope = _serviceProvider.CreateScope();
        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var dynamicEventHandlers = scope.ServiceProvider.GetRequiredService<IDynamicEventHandlers>();
        _tracker.ExpectedItemsCount = 2;

        // Act
        await eventBroker.Publish(new TestEventBase(1));
        await Task.Delay(TimeSpan.FromMilliseconds(50));

        IDynamicHandlerClaimTicket claimTicket = dynamicEventHandlers.Add(handlerRegistryBuilder);
        await eventBroker.Publish(new TestEventBase(2));

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
        var handlerRegistryBuilder = new DelegateHandlerRegistryBuilder();
        handlerRegistryBuilder
            .RegisterHandler<TestEventBase>(async static (TestEventBase testEvent, EventsTracker tracker) => await tracker.TrackAsync(testEvent))
            .Builder()
            .RegisterHandler<TestEventBase>(async static (TestEventBase testEvent, EventsTracker tracker) => await tracker.TrackAsync(testEvent));

        using var scope = _serviceProvider.CreateScope();
        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var dynamicEventHandlers = scope.ServiceProvider.GetRequiredService<IDynamicEventHandlers>();
        _tracker.ExpectedItemsCount = 2;

        // Act
        await eventBroker.Publish(new TestEventBase(1));
        await Task.Delay(TimeSpan.FromMilliseconds(50));

        IDynamicHandlerClaimTicket claimTicket = dynamicEventHandlers.Add(handlerRegistryBuilder);
        await eventBroker.Publish(new TestEventBase(2));
        await Task.Delay(TimeSpan.FromMilliseconds(50));

        dynamicEventHandlers.Remove(claimTicket);

        await eventBroker.Publish(new TestEventBase(3));

        await _tracker.Wait(TimeSpan.FromMilliseconds(300));

        // Assert
        var items = _tracker.Items.Select(x => x.Item).OfType<TestEventBase>().ToArray();
        Assert.Equal(2, items.Length);
        Assert.Equal(2, items[0].Number);
        Assert.Equal(2, items[1].Number);

        _output.WriteLine($"Elapsed: {_tracker.Elapsed}");
    }

    [Fact]
    public async Task Multiple_Dynamic_HandlerRegistries_Added()
    {
        // Arrange
        var handlerRegistryBuilder1 = new DelegateHandlerRegistryBuilder();
        handlerRegistryBuilder1
            .RegisterHandler<TestEventBase>(async static (TestEventBase testEvent, EventsTracker tracker) => await tracker.TrackAsync(testEvent))
            .Builder()
            .RegisterHandler<TestEventBase>(async static (TestEventBase testEvent, EventsTracker tracker) => await tracker.TrackAsync(testEvent));

        var handlerRegistryBuilder2 = new DelegateHandlerRegistryBuilder();
        handlerRegistryBuilder2
            .RegisterHandler<TestEventBase>(async static (TestEventBase testEvent, EventsTracker tracker) => await tracker.TrackAsync(testEvent));

        using var scope = _serviceProvider.CreateScope();
        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var dynamicEventHandlers = scope.ServiceProvider.GetRequiredService<IDynamicEventHandlers>();
        _tracker.ExpectedItemsCount = 3;

        // Act
        await eventBroker.Publish(new TestEventBase(1));
        await Task.Delay(TimeSpan.FromMilliseconds(50));

        IDynamicHandlerClaimTicket claimTicket1 = dynamicEventHandlers.Add(handlerRegistryBuilder1);
        IDynamicHandlerClaimTicket claimTicket2 = dynamicEventHandlers.Add(handlerRegistryBuilder2);
        await Task.Delay(TimeSpan.FromMilliseconds(50));

        await eventBroker.Publish(new TestEventBase(2));
        
        await _tracker.Wait(TimeSpan.FromMilliseconds(300));

        // Assert
        var items = _tracker.Items.Select(x => x.Item).OfType<TestEventBase>().ToArray();
        Assert.Equal(3, items.Length);
        Assert.Equal(2, items[0].Number);
        Assert.Equal(2, items[1].Number);
        Assert.Equal(2, items[2].Number);

        _output.WriteLine($"Elapsed: {_tracker.Elapsed}");
    }

    [Fact]
    public async Task Multiple_Dynamic_HandlerRegistries_Removed()
    {
        // Arrange
        var handlerRegistryBuilder1 = new DelegateHandlerRegistryBuilder();
        handlerRegistryBuilder1
            .RegisterHandler<TestEventBase>(async static (TestEventBase testEvent, EventsTracker tracker) => await tracker.TrackAsync(testEvent))
            .Builder()
            .RegisterHandler<TestEventBase>(async static (TestEventBase testEvent, EventsTracker tracker) => await tracker.TrackAsync(testEvent));

        var handlerRegistryBuilder2 = new DelegateHandlerRegistryBuilder();
        handlerRegistryBuilder2
            .RegisterHandler<TestEventBase>(async static (TestEventBase testEvent, EventsTracker tracker) => await tracker.TrackAsync(testEvent));

        using var scope = _serviceProvider.CreateScope();
        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var dynamicEventHandlers = scope.ServiceProvider.GetRequiredService<IDynamicEventHandlers>();
        _tracker.ExpectedItemsCount = 3;

        // Act
        await eventBroker.Publish(new TestEventBase(1));
        await Task.Delay(TimeSpan.FromMilliseconds(50));

        IDynamicHandlerClaimTicket claimTicket1 = dynamicEventHandlers.Add(handlerRegistryBuilder1);
        IDynamicHandlerClaimTicket claimTicket2 = dynamicEventHandlers.Add(handlerRegistryBuilder2);
        await Task.Delay(TimeSpan.FromMilliseconds(50));

        await eventBroker.Publish(new TestEventBase(2));
        await Task.Delay(TimeSpan.FromMilliseconds(50));

        dynamicEventHandlers.RemoveRange([claimTicket1, claimTicket2]);

        await eventBroker.Publish(new TestEventBase(3));

        await _tracker.Wait(TimeSpan.FromMilliseconds(300));

        // Assert
        var items = _tracker.Items.Select(x => x.Item).OfType<TestEventBase>().ToArray();
        Assert.Equal(3, items.Length);
        Assert.Equal(2, items[0].Number);
        Assert.Equal(2, items[1].Number);
        Assert.Equal(2, items[2].Number);

        _output.WriteLine($"Elapsed: {_tracker.Elapsed}");
    }

    [Fact]
    public async Task Multiple_Dynamic_HandlerRegistries_RemoveSome()
    {
        // Arrange
        var handlerRegistryBuilder1 = new DelegateHandlerRegistryBuilder();
        handlerRegistryBuilder1
            .RegisterHandler<TestEventBase>(async static (TestEventBase testEvent, EventsTracker tracker) => await tracker.TrackAsync(testEvent))
            .Builder()
            .RegisterHandler<TestEventBase>(async static (TestEventBase testEvent, EventsTracker tracker) => await tracker.TrackAsync(testEvent));

        var handlerRegistryBuilder2 = new DelegateHandlerRegistryBuilder();
        handlerRegistryBuilder2
            .RegisterHandler<TestEventBase>(async static (TestEventBase testEvent, EventsTracker tracker) => await tracker.TrackAsync(testEvent));

        using var scope = _serviceProvider.CreateScope();
        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var dynamicEventHandlers = scope.ServiceProvider.GetRequiredService<IDynamicEventHandlers>();
        _tracker.ExpectedItemsCount = 4;

        // Act
        await eventBroker.Publish(new TestEventBase(1));
        await Task.Delay(TimeSpan.FromMilliseconds(50));

        IDynamicHandlerClaimTicket claimTicket1 = dynamicEventHandlers.Add(handlerRegistryBuilder1);
        IDynamicHandlerClaimTicket claimTicket2 = dynamicEventHandlers.Add(handlerRegistryBuilder2);
        await Task.Delay(TimeSpan.FromMilliseconds(50));

        await eventBroker.Publish(new TestEventBase(2));
        await Task.Delay(TimeSpan.FromMilliseconds(50));

        dynamicEventHandlers.Remove(claimTicket1);

        await eventBroker.Publish(new TestEventBase(3));

        await _tracker.Wait(TimeSpan.FromMilliseconds(300));

        // Assert
        var items = _tracker.Items.OrderBy(x => x.Timestamp).Select(x => x.Item).OfType<TestEventBase>().ToArray();
        Assert.Equal(4, items.Length);
        Assert.Equal(2, items[0].Number);
        Assert.Equal(2, items[1].Number);
        Assert.Equal(2, items[2].Number);
        Assert.Equal(3, items[3].Number);

        _output.WriteLine($"Elapsed: {_tracker.Elapsed}");
    }

    [Fact]
    public async Task Handler_Dynamically_Added_Supports_Wrappers()
    {
        // Arrange
        var handlerRegistryBuilder = new DelegateHandlerRegistryBuilder();
        handlerRegistryBuilder
            .RegisterHandler<TestEventBase>(
                async static (TestEventBase testEvent, EventsTracker tracker) =>
                {
                    await tracker.TrackAsync(testEvent);
                })
            .WrapWith(
                async static (TestEventBase testEvent, EventsTracker tracker, INextHandler next) =>
                {
                    await tracker.TrackAsync(2);
                    await next.Execute();
                })
            .WrapWith(
                async static (TestEventBase testEvent, EventsTracker tracker, INextHandler next) =>
                {
                    await tracker.TrackAsync(3);
                    await next.Execute();
                });

        using var scope = _serviceProvider.CreateScope();
        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var dynamicEventHandlers = scope.ServiceProvider.GetRequiredService<IDynamicEventHandlers>();
        _tracker.ExpectedItemsCount = 3;

        // Act
        await eventBroker.Publish(new TestEventBase(1));
        await Task.Delay(TimeSpan.FromMilliseconds(50));

        _ = dynamicEventHandlers.Add(handlerRegistryBuilder);
        await eventBroker.Publish(new TestEventBase(2));

        await _tracker.Wait(TimeSpan.FromMilliseconds(300));

        // Assert
        var items = _tracker.Items.Select(x => x.Item).OfType<TestEventBase>().ToArray();
        Assert.Single(items);
        Assert.Equal(2, items[0].Number);

        var wrapperItems = _tracker.Items.OrderBy(x => x.Timestamp).Select(x => x.Item).OfType<int>().ToArray();
        Assert.Equal(2, wrapperItems.Length);
        Assert.Equal(3, wrapperItems[0]);
        Assert.Equal(2, wrapperItems[1]);

        _output.WriteLine($"Elapsed: {_tracker.Elapsed}");
    }
}

public class DynamicEventHandlerExample : IDisposable
{
    private readonly IDynamicEventHandlers _dynamicEventHandlers;
    private readonly IDynamicHandlerClaimTicket _claimTicket;

    public DynamicEventHandlerExample(IDynamicEventHandlers dynamicEventHandlers)
    {
        _dynamicEventHandlers = dynamicEventHandlers;
        
        DelegateHandlerRegistryBuilder handlerRegistryBuilder = new();
        handlerRegistryBuilder
            .RegisterHandler<Event1>(
                async static (TestEventBase testEvent, EventsTracker tracker) => await tracker.TrackAsync(testEvent))
            .Builder()
            .RegisterHandler<TestEventBase>(
                async static (TestEventBase testEvent, EventsTracker tracker) => await tracker.TrackAsync(testEvent));
        
        _claimTicket = _dynamicEventHandlers.Add(handlerRegistryBuilder);
    }

    private Task HandleEvent1(Event1 event1) => Task.CompletedTask;

    private Task HandleEvent2(Event2 event2) => Task.CompletedTask;

    public void Dispose()
    {
        // Remove both event handlers using the IDynamicHandlerClaimTicket
        _dynamicEventHandlers.Remove(_claimTicket);
    }
}
