using Xunit.Abstractions;

namespace M.EventBrokerSlim.Tests.DelegateHandlerTests;

public class HandlerExecutionTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ServiceProvider _serviceProvider;
    private readonly DelegateHandlerRegistryBuilder _builder;
    private readonly EventsTracker _tracker;

    public HandlerExecutionTests(ITestOutputHelper output)
    {
        _output = output;
        _builder = new DelegateHandlerRegistryBuilder();
        _tracker = new EventsTracker();
        _serviceProvider = ServiceProviderHelper.Build(
            x => x.AddEventBroker()
                  .AddSingleton(_tracker)
                  .AddSingleton(_builder));
    }

    [Fact]
    public async Task Event_Injected_In_Handler()
    {
        // Arrange
        _builder.RegisterHandler<Event1>(static async (Event1 event1, EventsTracker tracker) => await tracker.TrackAsync(event1))
                .WrapWith(static async (Event1 event1, EventsTracker tracker, INextHandler next) =>
                {
                    tracker.Track(event1);
                    await next.Execute();
                });
        using var scope = _serviceProvider.CreateScope();
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
        _builder.RegisterHandler<Event1>(static async (CancellationToken cancellationToken, EventsTracker tracker) => await tracker.TrackAsync(cancellationToken))
                .WrapWith(static async (CancellationToken cancellationToken, EventsTracker tracker, INextHandler next) =>
                {
                    tracker.Track(cancellationToken);
                    await next.Execute();
                });
        using var scope = _serviceProvider.CreateScope();
        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        _tracker.ExpectedItemsCount = 2;

        // Act
        await eventBroker.Publish(new Event1(1));
        await _tracker.Wait(TimeSpan.FromSeconds(1));

        // Assert
        var items = _tracker.Items.Select(x => x.Item).OfType<CancellationToken>().ToArray();
        Assert.Equal(2, items.Length);
        Assert.Single(items.Distinct());

        _output.WriteLine($"Elapsed: {_tracker.Elapsed}");
    }

    [Fact]
    public async Task RetryPolicy_Injected_In_Handler()
    {
        // Arrange
        _builder.RegisterHandler<Event1>(static async (IRetryPolicy retryPolicy, EventsTracker tracker) => await tracker.TrackAsync(retryPolicy))
                .WrapWith(static async (IRetryPolicy retryPolicy, EventsTracker tracker, INextHandler next) =>
                {
                    tracker.Track(retryPolicy);
                    await next.Execute();
                });
        using var scope = _serviceProvider.CreateScope();
        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        _tracker.ExpectedItemsCount = 2;

        // Act
        await eventBroker.Publish(new Event1(1));
        await _tracker.Wait(TimeSpan.FromSeconds(1));

        // Assert
        var items = _tracker.Items.Select(x => x.Item).OfType<IRetryPolicy>().ToArray();
        Assert.Equal(2, items.Length);
        Assert.Single(items.Distinct());

        _output.WriteLine($"Elapsed: {_tracker.Elapsed}");
    }

    [Fact]
    public async Task Wrappers_Executed_In_Outer_To_Inner_Order()
    {
        // Arrange
        _builder.RegisterHandler<Event1>(static async (Event1 event1, EventsTracker tracker) => await tracker.TrackAsync("handler"))
                .WrapWith(static async (Event1 event1, EventsTracker tracker, INextHandler next) =>
                {
                    tracker.Track("wrapper1");
                    await next.Execute();
                })
                .WrapWith(static async (Event1 event1, EventsTracker tracker, INextHandler next) =>
                {
                    tracker.Track("wrapper2");
                    await next.Execute();
                })
                .WrapWith(static async (Event1 event1, EventsTracker tracker, INextHandler next) =>
                {
                    tracker.Track("wrapper3");
                    await next.Execute();
                });
        using var scope = _serviceProvider.CreateScope();
        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        _tracker.ExpectedItemsCount = 4;

        // Act
        await eventBroker.Publish(new Event1(1));
        await _tracker.Wait(TimeSpan.FromSeconds(1));

        // Assert
        var items = _tracker.Items.OrderBy(x => x.Timestamp).Select(x => x.Item).OfType<string>().ToArray();
        Assert.Equal(new[] { "wrapper3", "wrapper2", "wrapper1", "handler" }, items);

        _output.WriteLine($"Elapsed: {_tracker.Elapsed}");
    }

    [Fact]
    public async Task Calling_NextHandler_Multiple_Times_Has_No_Effect()
    {
        // Arrange
        _builder.RegisterHandler<Event1>(static async (Event1 event1, EventsTracker tracker) => await tracker.TrackAsync("handler"))
                .WrapWith(static async (Event1 event1, EventsTracker tracker, INextHandler next) =>
                {
                    tracker.Track("wrapper1");
                    await next.Execute();
                    await next.Execute();
                    await next.Execute();
                })
                .WrapWith(static async (Event1 event1, EventsTracker tracker, INextHandler next) =>
                {
                    tracker.Track("wrapper2");
                    await next.Execute();
                    await next.Execute();
                })
                .WrapWith(static async (Event1 event1, EventsTracker tracker, INextHandler next) =>
                {
                    tracker.Track("wrapper3");
                    await next.Execute();
                    await next.Execute();
                    await next.Execute();
                });
        using var scope = _serviceProvider.CreateScope();
        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        _tracker.ExpectedItemsCount = 4;

        // Act
        await eventBroker.Publish(new Event1(1));
        await _tracker.Wait(TimeSpan.FromSeconds(1));

        // Assert
        var items = _tracker.Items.OrderBy(x => x.Timestamp).Select(x => x.Item).OfType<string>().ToArray();
        Assert.Equal(new[] { "wrapper3", "wrapper2", "wrapper1", "handler" }, items);

        _output.WriteLine($"Elapsed: {_tracker.Elapsed}");
    }

    [Fact]
    public async Task Calling_Next_FromHandler_Has_No_Effect()
    {
        // Arrange
        _builder.RegisterHandler<Event1>(static async (Event1 event1, INextHandler next, EventsTracker tracker) =>
        {
            tracker.Track(event1);
            await next.Execute();
        });

        using var scope = _serviceProvider.CreateScope();
        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        _tracker.ExpectedItemsCount = 1;

        // Act
        await eventBroker.Publish(new Event1(1));
        await _tracker.Wait(TimeSpan.FromSeconds(1));

        // Assert
        var items = _tracker.Items.Select(x => x.Item).OfType<Event1>().ToArray();
        Assert.Single(items);

        _output.WriteLine($"Elapsed: {_tracker.Elapsed}");
    }

    [Fact]
    public async Task When_Wrapper_Does_Not_Call_NextExecute_Handler_Not_Executed()
    {
        // Arrange
        _builder.RegisterHandler<Event1>(static async (Event1 event1, EventsTracker tracker) => await tracker.TrackAsync(event1))
                .WrapWith(static async (Event1 event1, EventsTracker tracker, INextHandler next) => await tracker.TrackAsync(event1));
        using var scope = _serviceProvider.CreateScope();
        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        _tracker.ExpectedItemsCount = 2;

        // Act
        await eventBroker.Publish(new Event1(1));
        await _tracker.Wait(TimeSpan.FromSeconds(1));

        // Assert
        var items = _tracker.Items.Select(x => x.Item).OfType<Event1>().ToArray();
        Assert.Single(items);

        _output.WriteLine($"Elapsed: {_tracker.Elapsed}");
    }

    [Fact]
    public async Task Deferred_Publish_Is_Handled()
    {
        // Arrange
        _builder.RegisterHandler<Event1>(static async (Event1 event1, EventsTracker tracker) => await tracker.TrackAsync(event1))
                .WrapWith(static async (Event1 event1, EventsTracker tracker, INextHandler next) =>
                {
                    tracker.Track(event1);
                    await next.Execute();
                });
        using var scope = _serviceProvider.CreateScope();
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
        _builder.RegisterHandler<Event1>(static async (Event1 event1, IRetryPolicy retryPolicy, EventsTracker tracker) =>
        {
            await tracker.TrackAsync(event1);
            if(retryPolicy.Attempt < 2)
            {
                retryPolicy.RetryAfter(TimeSpan.FromMilliseconds(100));
            }
        });
        using var scope = _serviceProvider.CreateScope();
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
        _builder.RegisterHandler<Event1>(static async (Event1 event1, IRetryPolicy retryPolicy, EventsTracker tracker) => await tracker.TrackAsync("handler"))
                .WrapWith(static async (Event1 event1, IRetryPolicy retryPolicy, EventsTracker tracker, INextHandler next) =>
                {
                    await next.Execute();
                    tracker.Track(event1);
                    if(retryPolicy.Attempt < 2)
                    {
                        retryPolicy.RetryAfter(TimeSpan.FromMilliseconds(100));
                    }
                });
        using var scope = _serviceProvider.CreateScope();
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

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }
}
