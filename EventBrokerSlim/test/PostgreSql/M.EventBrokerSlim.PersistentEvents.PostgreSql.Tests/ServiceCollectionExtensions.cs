using M.EventBrokerSlim.DependencyInjection;
using M.EventBrokerSlim.PersistentEvents.PostgreSql.Tests;
using Microsoft.Extensions.DependencyInjection;
using PostgreSqlIntegrationTests;

namespace M.EventBrokerSlim.PersistentEvents.PostgreSql.Tests;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEventBroker(this IServiceCollection services, Setup setup)
        => services
            .AddEventBroker(x => x.WithPostgreSqlPersistence((db, cfg) =>
            {
                db.ConnectionString = setup.ConnectionString;
                db.CreateEventsTable();
            }))
            .AddSingleton(EventRegistryHelper.Registry)
            .AddSingleton<EventReceiver>();
}
