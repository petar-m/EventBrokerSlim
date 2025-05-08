using FuncPipeline;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace M.EventBrokerSlim.Tests.DelegateHandlerTests;

public class HandlerExecutionTests
{
    private readonly ITestOutputHelper _output;
    private readonly ServiceCollection _serviceCollection;
    private readonly EventsTracker _tracker;

    public HandlerExecutionTests(ITestOutputHelper output)
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
    public async Task Event_Injected_In_Handler()
    {
        // Arrange
        PipelineBuilder.Create()
            .NewPipeline()
            .Execute(static async (Event1 event1, EventsTracker tracker, INext next) =>
            {
                tracker.Track(event1);
                await next.RunAsync();
            })
            .Execute(static async (Event1 event1, EventsTracker tracker) => await tracker.TrackAsync(event1))
            .Build(x => _serviceCollection.AddEventHandlerPileline<Event1>(x));

        using var services = _serviceCollection.BuildServiceProvider(true);
        using var scope = services.CreateScope();
        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        _tracker.ExpectedItemsCount = 2;
        var event1 = new Event1(1);

        // Act
        await eventBroker.Publish(event1);
        await _tracker.Wait(TimeSpan.FromSeconds(1));

        // Assert
        var items = _tracker.Items.Select(x => x.Item).OfType<Event1>().ToArray();
        Assert.Equal(2, items.Length);
        Assert.Single(items.Distinct(), event1);

        _output.WriteLine($"Elapsed: {_tracker.Elapsed}");
    }

    [Fact]
    public async Task CancellationToken_Injected_In_Handler()
    {
        // Arrange
        PipelineBuilder.Create()
            .NewPipeline()
            .Execute(static async (CancellationToken cancellationToken, EventsTracker tracker, INext next) =>
            {
                tracker.Track(cancellationToken);
                await next.RunAsync();
            })
            .Execute(static async (CancellationToken cancellationToken, EventsTracker tracker) => await tracker.TrackAsync(cancellationToken))
            .Build(x => _serviceCollection.AddEventHandlerPileline<Event1>(x));

        using var services = _serviceCollection.BuildServiceProvider(true);
        using var scope = services.CreateScope();
        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        _tracker.ExpectedItemsCount = 2;

        // Act
        await eventBroker.Publish(new Event1(1));
        await _tracker.Wait(TimeSpan.FromSeconds(1));

        // Assert
        var items = _tracker.Items.Select(x => x.Item).OfType<CancellationToken>().ToArray();
        Assert.Equal(2, items.Length);
        Assert.Single(items.Distinct());
        Assert.NotEqual(default, items[0]);

        _output.WriteLine($"Elapsed: {_tracker.Elapsed}");
    }

    [Fact]
    public async Task RetryPolicy_Injected_In_Handler()
    {
        // Arrange
        PipelineBuilder.Create()
            .NewPipeline()
            .Execute(static async (IRetryPolicy retryPolicy, EventsTracker tracker, INext next) =>
            {
                tracker.Track(retryPolicy.GetHashCode());
                await next.RunAsync();
            })
            .Execute(static async (IRetryPolicy retryPolicy, EventsTracker tracker) => await tracker.TrackAsync(retryPolicy.GetHashCode()))
            .Build(x => _serviceCollection.AddEventHandlerPileline<Event1>(x));

        using var services = _serviceCollection.BuildServiceProvider(true);
        using var scope = services.CreateScope();
        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        _tracker.ExpectedItemsCount = 2;

        // Act
        await eventBroker.Publish(new Event1(1));
        await _tracker.Wait(TimeSpan.FromSeconds(1));

        // Assert
        var items = _tracker.Items.Select(x => x.Item).OfType<int>().ToArray();
        Assert.Equal(2, items.Length);
        Assert.Single(items.Distinct());

        _output.WriteLine($"Elapsed: {_tracker.Elapsed}");
    }

    [Fact]
    public async Task Wrappers_Executed_In_Order()
    {
        // Arrange
        PipelineBuilder.Create()
            .NewPipeline()
            .Execute(static async (Event1 event1, EventsTracker tracker, INext next) =>
            {
                tracker.Track("before wrapper1");
                await next.RunAsync();
                tracker.Track("after wrapper1");
            })
            .Execute(static async (Event1 event1, EventsTracker tracker, INext next) =>
            {
                tracker.Track("before wrapper2");
                await next.RunAsync();
                tracker.Track("after wrapper2");
            })
            .Execute(static async (Event1 event1, EventsTracker tracker, INext next) =>
            {
                tracker.Track("before wrapper3");
                await next.RunAsync();
                tracker.Track("after wrapper3");
            })
            .Execute(static async (Event1 event1, EventsTracker tracker) => await tracker.TrackAsync("handler"))
            .Build(x => _serviceCollection.AddEventHandlerPileline<Event1>(x));

        using var services = _serviceCollection.BuildServiceProvider(true);
        using var scope = services.CreateScope();
        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        _tracker.ExpectedItemsCount = 7;

        // Act
        await eventBroker.Publish(new Event1(1));
        await _tracker.Wait(TimeSpan.FromSeconds(1));

        // Assert
        var items = _tracker.Items.OrderBy(x => x.Timestamp).Select(x => x.Item).OfType<string>().ToArray();
        Assert.Equal(new[] { "before wrapper1", "before wrapper2", "before wrapper3", "handler", "after wrapper3", "after wrapper2", "after wrapper1" }, items);

        _output.WriteLine($"Elapsed: {_tracker.Elapsed}");
    }

    [Fact]
    public async Task Deferred_Publish_Is_Handled()
    {
        // Arrange
        PipelineBuilder.Create()
            .NewPipeline()
            .Execute(static async (Event1 event1, EventsTracker tracker, INext next) =>
            {
                tracker.Track(event1);
                await next.RunAsync();
            })
            .Execute(static async (Event1 event1, EventsTracker tracker) => await tracker.TrackAsync(event1))
            .Build(x => _serviceCollection.AddEventHandlerPileline<Event1>(x));

        using var services = _serviceCollection.BuildServiceProvider(true);
        using var scope = services.CreateScope();
        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        _tracker.ExpectedItemsCount = 2;

        // Act
        await eventBroker.PublishDeferred(new Event1(1), TimeSpan.FromMilliseconds(200));
        await _tracker.Wait(TimeSpan.FromSeconds(1));

        // Assert
        var items = _tracker.Items.Select(x => x.Item).OfType<Event1>().ToArray();
        Assert.Equal(2, items.Length);

        _output.WriteLine($"Elapsed: {_tracker.Elapsed}");
    }

    [Fact]
    public async Task Retry_From_Handler()
    {
        // Arrange
        PipelineBuilder.Create()
            .NewPipeline()
            .Execute(static async (Event1 event1, IRetryPolicy retryPolicy, EventsTracker tracker) =>
            {
                await tracker.TrackAsync(event1);
                if(retryPolicy.Attempt < 2)
                {
                    retryPolicy.RetryAfter(TimeSpan.FromMilliseconds(100));
                }
            })
            .Build(x => _serviceCollection.AddEventHandlerPileline<Event1>(x));

        using var services = _serviceCollection.BuildServiceProvider(true);
        using var scope = services.CreateScope();
        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();

        _tracker.ExpectedItemsCount = 3;

        // Act
        await eventBroker.Publish(new Event1(1));
        await _tracker.Wait(TimeSpan.FromSeconds(1));

        // Assert
        var items = _tracker.Items.OrderBy(x => x.Timestamp).Select(x => x.Item).OfType<Event1>().ToArray();
        Assert.Equal(3, items.Length);
        var timestamps = _tracker.Items.OrderBy(x => x.Timestamp).Select(x => x.Timestamp).ToArray();
        for(int i = timestamps.Length - 1; i == 1; i--)
        {
            Assert.Equal(100d, (timestamps[i] - timestamps[i - 1]).TotalMilliseconds, 50d);
        }

        _output.WriteLine($"Elapsed: {_tracker.Elapsed}");
    }

    [Fact]
    public async Task Retry_From_Wrapper()
    {
        // Arrange
        PipelineBuilder.Create()
            .NewPipeline()
            .Execute(static async (Event1 event1, IRetryPolicy retryPolicy, EventsTracker tracker, INext next) =>
            {
                await next.RunAsync();
                tracker.Track(event1);
                if(retryPolicy.Attempt < 2)
                {
                    retryPolicy.RetryAfter(TimeSpan.FromMilliseconds(100));
                }
            })
            .Execute(static async (Event1 event1, IRetryPolicy retryPolicy, EventsTracker tracker) => await tracker.TrackAsync("handler"))
            .Build(x => _serviceCollection.AddEventHandlerPileline<Event1>(x));

        using var services = _serviceCollection.BuildServiceProvider(true);
        using var scope = services.CreateScope();
        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();

        _tracker.ExpectedItemsCount = 6;

        // Act
        await eventBroker.Publish(new Event1(1));
        await _tracker.Wait(TimeSpan.FromSeconds(1));

        // Assert

        var handler = _tracker.Items.Select(x => x.Item).OfType<string>().Where(x => x == "handler").ToArray();
        Assert.Equal(3, handler.Length);

        var items = _tracker.Items.Select(x => x.Item).OfType<Event1>().ToArray();
        Assert.Equal(3, items.Length);

        var timestamps = _tracker.Items.OrderBy(x => x.Timestamp).Select(x => x.Timestamp).ToArray();
        for(int i = timestamps.Length - 1; i == 1; i--)
        {
            Assert.Equal(100d, (timestamps[i] - timestamps[i - 1]).TotalMilliseconds, 50d);
        }

        _output.WriteLine($"Elapsed: {_tracker.Elapsed}");
    }
}
