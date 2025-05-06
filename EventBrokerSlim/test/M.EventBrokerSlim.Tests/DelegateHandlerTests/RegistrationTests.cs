using Xunit.Abstractions;

namespace M.EventBrokerSlim.Tests.DelegateHandlerTests;

public class RegistrationTests
{
    private readonly DelegateHandlerRegistryBuilder _builder;
    private readonly ITestOutputHelper _output;
    private readonly EventsTracker _eventsTracker;

    public RegistrationTests(ITestOutputHelper output)
    {
        _output = output;
        _builder = new DelegateHandlerRegistryBuilder();
        _eventsTracker = new EventsTracker();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(16)]
    public async Task Handler_Parameters_Are_Resolved_From_Container(int handlerParametersCount)
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithLogger(
            sc => sc.AddEventBroker()
                    .AddSingleton(_builder)
                    .AddSingleton(_eventsTracker)
                    .AddAllTestTypes());

        _builder.RegisterHandler<Event1>(GetHandler(handlerParametersCount, _eventsTracker));

        _eventsTracker.ExpectedItemsCount = 1;

        using var scope = services.CreateScope();
        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();

        // Act
        await eventBroker.Publish(new Event1(1));

        await _eventsTracker.Wait(TimeSpan.FromSeconds(1));

        // Assert
        Assert.Single(_eventsTracker.Items);
        _output.WriteLine($"Elapsed: {_eventsTracker.Elapsed}");
    }

    [Fact]
    public void When_Handler_Parameters_Exceed_Limit_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() => _builder.RegisterHandler<Event1>(GetHandler(17, _eventsTracker)));
        Assert.Equal("Delegate can't have more than 16 arguments.", exception.Message);
    }

    [Fact]
    public void When_Delegate_Does_Not_Return_Task_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() => _builder.RegisterHandler<Event1>(() => 1));
        Assert.Equal("Delegate must return a Task.", exception.Message);
    }

    [Fact]
    public async Task Registration_After_EvenBroker_Is_Created_Throws()
    {
        // Arrange
        var services = ServiceProviderHelper.Build(sc => sc.AddEventBroker().AddSingleton(_builder));

        using var scope = services.CreateScope();
        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        await eventBroker.Publish(new Event1(1));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => _builder.RegisterHandler<Event1>(() => Task.CompletedTask));

        // Assert
        Assert.Equal("Registry is closed. Please complete registrations before IEventBroker is resolved.", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(16)]
    public async Task Wrapper_Parameters_Are_Resolved_From_Container(int handlerParametersCount)
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithLogger(
            sc => sc.AddEventBroker()
                    .AddSingleton(_builder)
                    .AddSingleton(_eventsTracker)
                    .AddAllTestTypes());

        _builder.RegisterHandler<Event1>(() => Task.CompletedTask)
                .WrapWith(GetHandler(handlerParametersCount, _eventsTracker));

        _eventsTracker.ExpectedItemsCount = 1;

        using var scope = services.CreateScope();
        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();

        // Act
        await eventBroker.Publish(new Event1(1));

        await _eventsTracker.Wait(TimeSpan.FromSeconds(1));

        // Assert
        Assert.Single(_eventsTracker.Items);
        _output.WriteLine($"Elapsed: {_eventsTracker.Elapsed}");
    }

    [Fact]
    public void When_Wrapper_Parameters_Exceed_Limit_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() => _builder.RegisterHandler<Event1>(() => Task.CompletedTask).WrapWith(GetHandler(17, _eventsTracker)));
        Assert.Equal("Delegate can't have more than 16 arguments.", exception.Message);
    }

    [Fact]
    public void When_Wrapper_Does_Not_Return_Task_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() => _builder.RegisterHandler<Event1>(() => Task.CompletedTask).WrapWith(() => 1));
        Assert.Equal("Delegate must return a Task.", exception.Message);
    }

    [Fact]
    public async Task Wrapper_Registration_After_EvenBroker_Is_Created_Throws()
    {
        // Arrange
        var services = ServiceProviderHelper.Build(sc => sc.AddEventBroker().AddSingleton(_builder));

        var handler = _builder.RegisterHandler<Event1>(() => Task.CompletedTask);

        using var scope = services.CreateScope();
        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();
        await eventBroker.Publish(new Event1(1));

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => handler.WrapWith(() => Task.CompletedTask));

        // Assert
        Assert.Equal("Registry is closed. Please complete registrations before IEventBroker is resolved.", exception.Message);
    }

    [Fact]
    public async Task No_Builder_Registered()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithLogger(
            sc => sc.AddEventBroker(x => x.AddScoped<Event1, Handler1>())
                    .AddSingleton(_eventsTracker));

        using var scope = services.CreateScope();
        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();

        _eventsTracker.ExpectedItemsCount = 1;

        // Act
        await eventBroker.Publish(new Event1(1));

        await _eventsTracker.Wait(TimeSpan.FromSeconds(1));

        // Assert
        Assert.Single(_eventsTracker.Items);
    }

    [Fact]
    public async Task Multiple_Builders_Registered()
    {
        _builder.RegisterHandler<Event1>(async (EventsTracker tracker) => await tracker.TrackAsync("handler1"));

        var builder2 = new DelegateHandlerRegistryBuilder();
        builder2.RegisterHandler<Event1>(async (EventsTracker tracker) => await tracker.TrackAsync("handler2"));

        var builder3 = new DelegateHandlerRegistryBuilder();
        builder3.RegisterHandler<Event1>(async (EventsTracker tracker) => await tracker.TrackAsync("handler3"));

        // Arrange
        var services = ServiceProviderHelper.BuildWithLogger(
            sc => sc.AddEventBroker()
                    .AddSingleton(_builder)
                    .AddSingleton(builder2)
                    .AddSingleton(builder3)
                    .AddSingleton(_eventsTracker));

        using var scope = services.CreateScope();
        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();

        _eventsTracker.ExpectedItemsCount = 3;

        // Act
        await eventBroker.Publish(new Event1(1));

        await _eventsTracker.Wait(TimeSpan.FromSeconds(1));

        // Assert
        Assert.Equal(3, _eventsTracker.Items.Count);
        Assert.Equal(new[] { "handler1", "handler2", "handler3" }, _eventsTracker.Items.Select(x => x.Item).Cast<string>().Order().ToArray());
    }

#pragma warning disable RCS1163 // Unused parameter
    private static Delegate GetHandler(int parametersCount, EventsTracker _eventsTracker)
        => parametersCount switch
        {
            0 => async () => await _eventsTracker.TrackAsync(1),
            1 => async (A1 a1) => await _eventsTracker.TrackAsync(1),
            2 => async (A1 a1, A2 a2) => await _eventsTracker.TrackAsync(1),
            3 => async (A1 a1, A2 a2, A3 a3) => await _eventsTracker.TrackAsync(1),
            4 => async (A1 a1, A2 a2, A3 a3, A4 a4) => await _eventsTracker.TrackAsync(1),
            5 => async (A1 a1, A2 a2, A3 a3, A4 a4, A4 a5) => await _eventsTracker.TrackAsync(1),
            6 => async (A1 a1, A2 a2, A3 a3, A4 a4, A4 a5, A4 a6) => await _eventsTracker.TrackAsync(1),
            7 => async (A1 a1, A2 a2, A3 a3, A4 a4, A4 a5, A4 a6, A7 a7) => await _eventsTracker.TrackAsync(1),
            8 => async (A1 a1, A2 a2, A3 a3, A4 a4, A4 a5, A4 a6, A7 a7, A8 a8) => await _eventsTracker.TrackAsync(1),
            9 => async (A1 a1, A2 a2, A3 a3, A4 a4, A4 a5, A4 a6, A7 a7, A8 a8, A9 a9) => await _eventsTracker.TrackAsync(1),
            10 => async (A1 a1, A2 a2, A3 a3, A4 a4, A4 a5, A4 a6, A7 a7, A8 a8, A9 a9, A10 a10) => await _eventsTracker.TrackAsync(1),
            11 => async (A1 a1, A2 a2, A3 a3, A4 a4, A4 a5, A4 a6, A7 a7, A8 a8, A9 a9, A10 a10, A11 a11) => await _eventsTracker.TrackAsync(1),
            12 => async (A1 a1, A2 a2, A3 a3, A4 a4, A4 a5, A4 a6, A7 a7, A8 a8, A9 a9, A10 a10, A11 a11, A12 a12) => await _eventsTracker.TrackAsync(1),
            13 => async (A1 a1, A2 a2, A3 a3, A4 a4, A4 a5, A4 a6, A7 a7, A8 a8, A9 a9, A10 a10, A11 a11, A12 a12, A13 a13) => await _eventsTracker.TrackAsync(1),
            14 => async (A1 a1, A2 a2, A3 a3, A4 a4, A4 a5, A4 a6, A7 a7, A8 a8, A9 a9, A10 a10, A11 a11, A12 a12, A13 a13, A14 a14) => await _eventsTracker.TrackAsync(1),
            15 => async (A1 a1, A2 a2, A3 a3, A4 a4, A4 a5, A4 a6, A7 a7, A8 a8, A9 a9, A10 a10, A11 a11, A12 a12, A13 a13, A14 a14,  A15 a15) => await _eventsTracker.TrackAsync(1),
            16 => async (A1 a1, A2 a2, A3 a3, A4 a4, A4 a5, A4 a6, A7 a7, A8 a8, A9 a9, A10 a10, A11 a11, A12 a12, A13 a13, A14 a14,  A15 a15, A16 a16) => await _eventsTracker.TrackAsync(1),
            17 => async (A1 a1, A2 a2, A3 a3, A4 a4, A4 a5, A4 a6, A7 a7, A8 a8, A9 a9, A10 a10, A11 a11, A12 a12, A13 a13, A14 a14,  A15 a15, A16 a16, A17 a17) => await _eventsTracker.TrackAsync(1),
            _ => throw new NotImplementedException(),
        };
#pragma warning restore RCS1163 // Unused parameter

    class Handler1(EventsTracker tracker) : IEventHandler<Event1>
    {
        public async Task Handle(Event1 @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken) => await tracker.TrackAsync(@event);

        public Task OnError(Exception exception, Event1 @event, IRetryPolicy retryPolicy, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
