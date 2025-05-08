using Microsoft.Extensions.Logging;

namespace M.EventBrokerSlim.Tests;

public static class ServiceProviderHelper
{
    public static ServiceProvider BuildWithEventsRecorder<T>(Action<IServiceCollection> configure) where T : notnull
    {
        ServiceCollection serviceCollection = CreateServiceCollection(configure);
        serviceCollection.AddSingleton<EventsRecorder<T>>();
        return serviceCollection.BuildServiceProvider(true);
    }

    public static ServiceCollection NewWithLogger()
    {
        ServiceCollection serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging(x => x.AddDebug().AddTest());
        return serviceCollection;
    }

    private static ServiceCollection CreateServiceCollection(Action<IServiceCollection> configure)
    {
        var serviceCollection = new ServiceCollection();
        configure(serviceCollection);
        return serviceCollection;
    }
}
