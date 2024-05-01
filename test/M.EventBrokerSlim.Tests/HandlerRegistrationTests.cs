namespace M.EventBrokerSlim.Tests;

public class HandlerRegistrationTests
{
    [Fact]
    public void RegisterHandlerAsTransient_AddsToServiceCollection()
    {
        var serviceCollection = new ServiceCollection()
            .AddEventBroker(
                x => x.AddTransient<TestEvent, TestEventHandler>());

        var serviceDescriptor = serviceCollection.Single(x => x.IsKeyedService && x.KeyedImplementationType == typeof(TestEventHandler));

        Assert.Equal(ServiceLifetime.Transient, serviceDescriptor.Lifetime);
    }

    [Fact]
    public void RegisterHandlerAsScoped_AddsToServiceCollection()
    {
        var serviceCollection = new ServiceCollection()
            .AddEventBroker(
                x => x.AddScoped<TestEvent, TestEventHandler>());

        var serviceDescriptor = serviceCollection.Single(x => x.IsKeyedService && x.KeyedImplementationType == typeof(TestEventHandler));

        Assert.Equal(ServiceLifetime.Scoped, serviceDescriptor.Lifetime);
    }

    [Fact]
    public void RegisterHandlerAsSingleton_AddsToServiceCollection()
    {
        var serviceCollection = new ServiceCollection()
            .AddEventBroker(
                x => x.AddSingleton<TestEvent, TestEventHandler>());

        var serviceDescriptor = serviceCollection.Single(x => x.IsKeyedService && x.KeyedImplementationType == typeof(TestEventHandler));

        Assert.Equal(ServiceLifetime.Singleton, serviceDescriptor.Lifetime);
    }

    [Fact]
    public void MaxConcurrentHandlers_SetTo_Zero_Throws()
    {
        var serviceCollection = new ServiceCollection();

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            paramName: "maxConcurrentHandlers",
            testCode: () => serviceCollection.AddEventBroker(x => x.WithMaxConcurrentHandlers(0)));

        Assert.Equal("MaxConcurrentHandlers should be greater than zero (Parameter 'maxConcurrentHandlers')", exception.Message);
    }

    [Fact]
    public void MaxConcurrentHandlers_SetTo_Negative_Throws()
    {
        var serviceCollection = new ServiceCollection();
        var rand = new Random();
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            paramName: "maxConcurrentHandlers",
            testCode: () => serviceCollection.AddEventBroker(x => x.WithMaxConcurrentHandlers(rand.Next(int.MinValue, -1))));

        Assert.Equal("MaxConcurrentHandlers should be greater than zero (Parameter 'maxConcurrentHandlers')", exception.Message);
    }

    [Fact]
    public async Task Handlers_RegisteredWith_AddEventBroker_AreExecuted()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithEventsRecorder<string>(
            sc => sc.AddEventBroker(
                        x => x.AddTransient<TestEvent, TestEventHandler>()
                              .AddScoped<TestEvent, TestEventHandler1>()
                              .AddScoped<TestEvent, TestEventHandler2>()));

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsRecorder = scope.ServiceProvider.GetRequiredService<EventsRecorder<string>>();

        // Act
        var testEvent = new TestEvent(CorrelationId: "1");

        eventsRecorder.Expect(
            $"1_{typeof(TestEventHandler).Name}",
            $"1_{typeof(TestEventHandler1).Name}",
            $"1_{typeof(TestEventHandler2).Name}");

        await eventBroker.Publish(testEvent);

        var completed = await eventsRecorder.WaitForExpected(timeout: TimeSpan.FromMilliseconds(100));

        // Assert
        Assert.True(completed);
    }

    [Fact]
    public async Task Handlers_RegisteredBefore_AddEventBroker_AreExecuted()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithEventsRecorder<string>(
            sc => sc.AddEventHandlers(
                        x => x.AddTransient<TestEvent, TestEventHandler>()
                              .AddScoped<TestEvent, TestEventHandler1>()
                              .AddScoped<TestEvent, TestEventHandler2>())
                    .AddEventBroker());

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsRecorder = scope.ServiceProvider.GetRequiredService<EventsRecorder<string>>();

        // Act
        var testEvent = new TestEvent(CorrelationId: "1");

        eventsRecorder.Expect(
            $"1_{typeof(TestEventHandler).Name}",
            $"1_{typeof(TestEventHandler1).Name}",
            $"1_{typeof(TestEventHandler2).Name}");

        await eventBroker.Publish(testEvent);

        var completed = await eventsRecorder.WaitForExpected(timeout: TimeSpan.FromMilliseconds(100));

        // Assert
        Assert.True(completed);
    }

    [Fact]
    public async Task Handlers_RegisteredAfter_AddEventBroker_AreExecuted()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithEventsRecorder<string>(
            sc => sc.AddEventBroker()
                    .AddEventHandlers(
                        x => x.AddTransient<TestEvent, TestEventHandler>()
                              .AddScoped<TestEvent, TestEventHandler1>()
                              .AddScoped<TestEvent, TestEventHandler2>()));

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsRecorder = scope.ServiceProvider.GetRequiredService<EventsRecorder<string>>();

        // Act
        var testEvent = new TestEvent(CorrelationId: "1");

        eventsRecorder.Expect(
            $"1_{typeof(TestEventHandler).Name}",
            $"1_{typeof(TestEventHandler1).Name}",
            $"1_{typeof(TestEventHandler2).Name}");

        await eventBroker.Publish(testEvent);

        var completed = await eventsRecorder.WaitForExpected(timeout: TimeSpan.FromMilliseconds(100));

        // Assert
        Assert.True(completed);
    }

    [Fact]
    public async Task Handlers_RegisteredBeforeAndAfter_AddEventBroker_AreExecuted()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithEventsRecorder<string>(
            sc => sc.AddEventHandlers(x => x.AddTransient<TestEvent, TestEventHandler>())
                    .AddEventBroker(x => x.AddScoped<TestEvent, TestEventHandler1>())
                    .AddEventHandlers(x => x.AddScoped<TestEvent, TestEventHandler2>()));

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var eventsRecorder = scope.ServiceProvider.GetRequiredService<EventsRecorder<string>>();

        // Act
        var testEvent = new TestEvent(CorrelationId: "1");

        eventsRecorder.Expect(
            $"1_{typeof(TestEventHandler).Name}",
            $"1_{typeof(TestEventHandler1).Name}",
            $"1_{typeof(TestEventHandler2).Name}");

        await eventBroker.Publish(testEvent);

        var completed = await eventsRecorder.WaitForExpected(timeout: TimeSpan.FromMilliseconds(100));

        // Assert
        Assert.True(completed);
    }

    public record TestEvent(string CorrelationId) : ITraceable<string>;

    public class TestEventHandler : IEventHandler<TestEvent>
    {
        private readonly EventsRecorder<string> _eventsRecorder;

        public TestEventHandler(EventsRecorder<string> eventsRecorder)
        {
            _eventsRecorder = eventsRecorder;
        }

        public Task Handle(TestEvent @event, RetryPolicy retryPolicy, CancellationToken cancellationToken)
        {
            _eventsRecorder.Notify($"{@event.CorrelationId}_{GetType().Name}");
            return Task.CompletedTask;
        }

        public Task OnError(Exception exception, TestEvent @event, RetryPolicy retryPolicy, CancellationToken cancellationToken)
        {
            _eventsRecorder.Notify(exception, @event);
            return Task.CompletedTask;
        }
    }

    public class TestEventHandler1 : TestEventHandler
    {
        public TestEventHandler1(EventsRecorder<string> eventsRecorder) : base(eventsRecorder)
        {
        }
    }

    public class TestEventHandler2 : TestEventHandler
    {
        public TestEventHandler2(EventsRecorder<string> eventsRecorder) : base(eventsRecorder)
        {
        }
    }
}
