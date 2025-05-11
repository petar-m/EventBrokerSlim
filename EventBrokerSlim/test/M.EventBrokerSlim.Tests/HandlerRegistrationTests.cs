namespace M.EventBrokerSlim.Tests;

public class HandlerRegistrationTests
{
    [Fact]
    public void RegisterHandlerAsTransient_AddsToServiceCollection()
    {
        var serviceCollection = new ServiceCollection()
            .AddEventBroker()
            .AddTransientEventHandler<TestEvent, TestEventHandler>();

        var serviceDescriptor = serviceCollection.Single(x => x.IsKeyedService && x.KeyedImplementationType == typeof(TestEventHandler));

        Assert.Equal(ServiceLifetime.Transient, serviceDescriptor.Lifetime);
    }

    [Fact]
    public void RegisterHandlerAsScoped_AddsToServiceCollection()
    {
        var serviceCollection = new ServiceCollection()
            .AddEventBroker()
            .AddScopedEventHandler<TestEvent, TestEventHandler>();

        var serviceDescriptor = serviceCollection.Single(x => x.IsKeyedService && x.KeyedImplementationType == typeof(TestEventHandler));

        Assert.Equal(ServiceLifetime.Scoped, serviceDescriptor.Lifetime);
    }

    [Fact]
    public void RegisterHandlerAsSingleton_AddsToServiceCollection()
    {
        var serviceCollection = new ServiceCollection()
            .AddEventBroker()
            .AddSingletonEventHandler<TestEvent, TestEventHandler>();

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

        Assert.Equal("MaxConcurrentHandlers should be greater than zero. (Parameter 'maxConcurrentHandlers')", exception.Message);
    }

    [Fact]
    public void MaxConcurrentHandlers_SetTo_Negative_Throws()
    {
        var serviceCollection = new ServiceCollection();
        var rand = new Random();
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            paramName: "maxConcurrentHandlers",
            testCode: () => serviceCollection.AddEventBroker(x => x.WithMaxConcurrentHandlers(rand.Next(int.MinValue, -1))));

        Assert.Equal("MaxConcurrentHandlers should be greater than zero. (Parameter 'maxConcurrentHandlers')", exception.Message);
    }

    [Fact]
    public async Task Handlers_RegisteredBefore_AddEventBroker_AreExecuted()
    {
        // Arrange
        using var services = new ServiceCollection()
            .AddTransientEventHandler<TestEvent, TestEventHandler>()
            .AddScopedEventHandler<TestEvent, TestEventHandler1>()
            .AddScopedEventHandler<TestEvent, TestEventHandler2>()
            .AddEventBroker()
            .AddSingleton<EventsRecorder<string>>()
            .BuildServiceProvider(true);
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

        var completed = await eventsRecorder.WaitForExpected(timeout: TimeSpan.FromSeconds(1));

        // Assert
        Assert.True(completed);
    }

    [Fact]
    public async Task Handlers_RegisteredAfter_AddEventBroker_AreExecuted()
    {
        // Arrange
        using var services = new ServiceCollection()
            .AddEventBroker()
            .AddTransientEventHandler<TestEvent, TestEventHandler>()
            .AddScopedEventHandler<TestEvent, TestEventHandler1>()
            .AddScopedEventHandler<TestEvent, TestEventHandler2>()
            .AddSingleton<EventsRecorder<string>>()
            .BuildServiceProvider(true);
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

        var completed = await eventsRecorder.WaitForExpected(timeout: TimeSpan.FromSeconds(1));

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

        public Task Handle(TestEvent @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken)
        {
            _eventsRecorder.Notify($"{@event.CorrelationId}_{GetType().Name}");
            return Task.CompletedTask;
        }

        public Task OnError(Exception exception, TestEvent @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken)
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
