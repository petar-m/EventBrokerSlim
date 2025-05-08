using FuncPipeline;
using MELT;
using Microsoft.Extensions.Logging;

namespace M.EventBrokerSlim.Tests.DelegateHandlerTests;

public class ExceptionHandlingTests
{
    [Fact]
    public async Task Exception_WhenResolvingHandlerParameters_IsLogged()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddEventBroker().AddLogging(x => x.AddTest());

        PipelineBuilder.Create()
            .NewPipeline()
            .Execute(([ResolveFrom(PrimarySource = Source.Services, Fallback = false, PrimaryNotFound = NotFoundBehavior.ThrowException)]string notRegistered) => Task.CompletedTask)
            .Build(x => serviceCollection.AddEventHandlerPileline<Event1>(x));

        using ServiceProvider services = serviceCollection.BuildServiceProvider(true);
        using IServiceScope scope = services.CreateScope();
        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();

        // Act
        await eventBroker.Publish(new Event1(1));

        await Task.Delay(TimeSpan.FromSeconds(1));

        // Assert
        var provider = (TestLoggerProvider)scope.ServiceProvider.GetServices<ILoggerProvider>().Single(x => x is TestLoggerProvider);

        var log = Assert.Single(provider.Sink.LogEntries);
        Assert.Equal(LogLevel.Error, log.LogLevel);
        Assert.Equal($"Unhandled exception executing handler for event {typeof(Event1).FullName}", log.Message);
        Assert.Equal("No service for type System.String has been registered. ResolveFromAttribute { PrimarySource = Services, Fallback = False, PrimaryNotFound = ThrowException, SecondaryNotFound = ReturnTypeDefault, Key =  }.", log.Exception?.Message);
    }

    [Fact]
    public async Task Unhandled_Exception_WhenExecuting_IsLogged()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddEventBroker().AddLogging(x => x.AddTest());

        var pipeline = PipelineBuilder.Create()
            .NewPipeline()
            .Execute(static (Event1 @event) => throw new NotImplementedException())
            .Build(x => serviceCollection.AddEventHandlerPileline<Event1>(x));

        using var services = serviceCollection.BuildServiceProvider(true);
        using var scope = services.CreateScope();

        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();

        // Act
        await eventBroker.Publish(new Event1(1));

        await Task.Delay(TimeSpan.FromSeconds(1));

        // Assert
        var provider = (TestLoggerProvider)scope.ServiceProvider.GetServices<ILoggerProvider>().Single(x => x is TestLoggerProvider);

        var log = Assert.Single(provider.Sink.LogEntries);
        Assert.Equal(LogLevel.Error, log.LogLevel);
        Assert.Equal($"Unhandled exception executing handler for event {typeof(Event1).FullName}", log.Message);
        Assert.Equal("The method or operation is not implemented.", log.Exception?.Message);
    }

    [Fact]
    public async Task Shutdown_During_Handling_TaskCanceledException_IsLogged()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddEventBroker().AddLogging(x => x.AddTest());

        var pipeline = PipelineBuilder.Create()
            .NewPipeline()
            .Execute(async (CancellationToken cancellationToken) => await Task.Delay(200, cancellationToken))
            .Build(x => serviceCollection.AddEventHandlerPileline<Event1>(x));

        using var services = serviceCollection.BuildServiceProvider(true);
        using var scope = services.CreateScope();
        var eventBroker = scope.ServiceProvider.GetRequiredService<IEventBroker>();

        // Act
        await eventBroker.Publish(new Event1(1));
        await Task.Delay(50);
        eventBroker.Shutdown();
        await Task.Delay(50);

        // Assert
        var provider = (TestLoggerProvider)scope.ServiceProvider.GetServices<ILoggerProvider>().Single(x => x is TestLoggerProvider);

        var log = Assert.Single(provider.Sink.LogEntries);
        Assert.Equal(LogLevel.Error, log.LogLevel);
        Assert.Equal($"Unhandled exception executing handler for event {typeof(Event1).FullName}", log.Message);
        Assert.Equal("A task was canceled.", log.Exception?.Message);
    }
}
