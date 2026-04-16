using FuncPipeline;
using M.EventBrokerSlim.DependencyInjection;
using M.EventBrokerSlim.Persistent;
using Microsoft.Extensions.DependencyInjection;
using PostgreSqlIntegrationTests;

namespace M.EventBrokerSlim.PersistentEvents.PostgreSql.Tests.Tests;

public class DeadLetteredAndCompletedHandlingTest : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly Setup _setup;
    private readonly IServiceScope _scope;

    public DeadLetteredAndCompletedHandlingTest(Setup setup)
    {
        _setup = setup;
        var services = new ServiceCollection()
            .AddEventBroker(x => x.WithPostgreSqlPersistence((db, cfg) =>
            {
                cfg.CompletedRecordTtl = TimeSpan.FromSeconds(2);
                cfg.DeadLetteredRecordTtl = TimeSpan.FromSeconds(2);
                cfg.DeleteCompletedAndDeadLetteredExceedingTtlExecuteInterval =
                new Jitter(
                    initial: new Jitter.Periodic(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(0)),
                    regular: new Jitter.Periodic(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)));

                db.ConnectionString = setup.ConnectionString;
                db.Schema = "ebs_10";
                db.Table = nameof(DeadLetteredAndCompletedHandlingTest);
                db.CreateEventsTable();
            }))
            .AddSingleton(EventRegistryHelper.Registry);

        var builder = PipelineBuilder.Create()
            .NewPipeline()
            .Execute(() => Task.CompletedTask) // produces status completed
            .Build()
            .NewPipeline()
            .Execute((IRetryPolicy retryPolicy) => // produces status dead lettered
            {
                retryPolicy.Abandon();
                return Task.CompletedTask;
            })
            .Build();

        services.AddEventHandlerPipeline<SampleEvent>(builder.Pipelines[0], handlerName: "handler-1");
        services.AddEventHandlerPipeline<SampleEvent>(builder.Pipelines[1], handlerName: "handler-2");

        _serviceProvider = services.BuildServiceProvider();
        _scope = _serviceProvider.CreateScope();
        _scope.ServiceProvider.UsePersistentEventBroker();
    }

    [Fact]
    public async Task Dead_lettered_and_completed_events_exceeding_TTL_are_deleted()
    {
        var eventBroker = _scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var sampleEvent = new SampleEvent("two handlers");

        await eventBroker.Publish(sampleEvent, TestContext.Current.CancellationToken);

        // give room to maintenance task to delete the events
        await Task.Delay(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        await AssertNoRowsAsync();
    }

    private async Task AssertNoRowsAsync()
    {
        using var connection = _setup.DataSource.CreateConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM ebs_10.{nameof(DeadLetteredAndCompletedHandlingTest)}";
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var count = (long?)await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, count);
    }
    public void Dispose()
    {
        _scope.ServiceProvider.GetRequiredService<IEventBroker>().Shutdown();
        _scope.Dispose();
        _serviceProvider.Dispose();
    }
}
