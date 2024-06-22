using MELT;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace M.EventBrokerSlim.Tests.DelegateHandlerTests;

public class ExceptionHandlingTests
{
    private readonly DelegateHandlerRegistryBuilder _builder;
    private readonly ITestOutputHelper _output;
    private readonly EventsTracker _eventsTracker;

    public ExceptionHandlingTests(ITestOutputHelper output)
    {
        _output = output;
        _builder = new DelegateHandlerRegistryBuilder();
        _eventsTracker = new EventsTracker();
    }

    [Fact]
    public async Task Exception_WhenResolvingHandlerParameters_IsLogged()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithLogger(
            sc => sc.AddEventBroker()
                    .AddSingleton(_builder)
                    .AddSingleton(_eventsTracker));

        _builder.RegisterHandler<Event1>((string notRegistered) => Task.CompletedTask);

        using var scope = services.CreateScope();
        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();

        // Act
        await eventBroker.Publish(new Event1(1));

        await _eventsTracker.Wait(TimeSpan.FromMilliseconds(100));

        // Assert
        _output.WriteLine($"Elapsed: {_eventsTracker.Elapsed}");
        var provider = (TestLoggerProvider)scope.ServiceProvider.GetServices<ILoggerProvider>().Single(x => x is TestLoggerProvider);

        var log = Assert.Single(provider.Sink.LogEntries);
        Assert.Equal(LogLevel.Error, log.LogLevel);
        Assert.Equal($"Unhandled exception executing delegate handler for event {typeof(Event1).FullName}", log.Message);
        Assert.Equal("No service for type 'System.String' has been registered.", log.Exception?.Message);
    }

    [Fact]
    public async Task Unhandled_Exception_WhenExecuting_IsLogged()
    {
        // Arrange
        var services = ServiceProviderHelper.BuildWithLogger(
            sc => sc.AddEventBroker()
                    .AddSingleton(_builder)
                    .AddSingleton(_eventsTracker));

        _builder.RegisterHandler<Event1>(async (Event1 @event, EventsTracker tracker) =>
        {
            await Task.CompletedTask;
            tracker.Track(@event);
            throw new NotImplementedException();
        });

        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();

        // Act
        await eventBroker.Publish(new Event1(1));

        await _eventsTracker.Wait(timeout: TimeSpan.FromMilliseconds(100));

        // Assert
        _output.WriteLine($"Elapsed: {_eventsTracker.Elapsed}");
        var provider = (TestLoggerProvider)scope.ServiceProvider.GetServices<ILoggerProvider>().Single(x => x is TestLoggerProvider);

        var log = Assert.Single(provider.Sink.LogEntries);
        Assert.Equal(LogLevel.Error, log.LogLevel);
        Assert.Equal($"Unhandled exception executing delegate handler for event {typeof(Event1).FullName}", log.Message);
        Assert.Equal("The method or operation is not implemented.", log.Exception?.Message);
    }
}
