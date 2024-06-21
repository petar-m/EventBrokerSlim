using Microsoft.Extensions.Logging;

namespace M.EventBrokerSlim.Tests;

public static class ServiceProviderHelper
{
    public static ServiceProvider Build(Action<IServiceCollection> configure)
    {
        ServiceCollection serviceCollection = CreateServiceCollection(configure);
        return serviceCollection.BuildServiceProvider(true);
    }

    public static ServiceProvider BuildWithEventsRecorder<T>(Action<IServiceCollection> configure) where T : notnull
    {
        ServiceCollection serviceCollection = CreateServiceCollection(configure);
        serviceCollection.AddSingleton<EventsRecorder<T>>();
        return serviceCollection.BuildServiceProvider(true);
    }

    public static ServiceProvider BuildWithEventsRecorderAndLogger<T>(Action<IServiceCollection> configure) where T : notnull
    {
        ServiceCollection serviceCollection = CreateServiceCollection(configure);
        serviceCollection.AddSingleton<EventsRecorder<T>>();
        serviceCollection.AddLogging(x => x.AddDebug().AddTest());
        return serviceCollection.BuildServiceProvider(true);
    }

    public static ServiceProvider BuildWithLogger(Action<IServiceCollection> configure)
    {
        ServiceCollection serviceCollection = CreateServiceCollection(configure);
        serviceCollection.AddLogging(x => x.AddDebug().AddTest());
        return serviceCollection.BuildServiceProvider(true);
    }

    private static ServiceCollection CreateServiceCollection(Action<IServiceCollection> configure)
    {
        var serviceCollection = new ServiceCollection();
        configure(serviceCollection);
        return serviceCollection;
    }
}
