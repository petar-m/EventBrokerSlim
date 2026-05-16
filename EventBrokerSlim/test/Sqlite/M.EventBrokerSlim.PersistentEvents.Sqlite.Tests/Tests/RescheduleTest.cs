using System;
using System.Threading.Tasks;
using FuncPipeline;
using M.EventBrokerSlim.DependencyInjection;
using M.EventBrokerSlim.Persistent;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using SqliteIntegrationTests;

namespace M.EventBrokerSlim.PersistentEvents.Sqlite.Tests.Tests;

public class RescheduleTest : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScope _scope;
    private readonly string _connectionString;

    public RescheduleTest(Setup setup)
    {
        _connectionString = setup.GetConnectionString();
        var builder = PipelineBuilder
            .Create()
            .NewPipeline()
            .Execute(async (EventRecord er, EventReceiver receiver, System.Threading.CancellationToken cancellationToken) =>
            {
                receiver.Add(er);
                await Task.Delay(TimeSpan.FromHours(1), cancellationToken);
            })
            .Build();
        var services = new ServiceCollection()
            .AddEventBroker(x => x.WithSqlitePersistence((db, cfg) =>
            {
                cfg.PollingInterval = TimeSpan.FromHours(1);
                cfg.ProcessingTimeout = TimeSpan.FromSeconds(1);
                cfg.RescheduleClaimedExceedingProcessingTimeoutExecuteInterval =
                new Jitter(
                    initial: new Jitter.Periodic(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(0)),
                    regular: new Jitter.Periodic(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)));

                db.ConnectionString = _connectionString;
                db.Table = nameof(RescheduleTest);
                db.CreateEventsTable();
            }))
            .AddEventHandlerPipeline<SampleEvent>(builder.Pipelines[0], o => o.WithHandlerName("sample-event-handler"))
            .AddSingleton(EventRegistryHelper.Registry)
            .AddSingleton<EventReceiver>();

        _serviceProvider = services.BuildServiceProvider();
        _serviceProvider.UsePersistentEventBroker();
        _scope = _serviceProvider.CreateScope();
    }

    [Fact]
    public async Task Event_exceeding_processing_timeout_is_rescheduled()
    {
        var broker = _scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var receiver = _scope.ServiceProvider.GetRequiredService<EventReceiver>();
        var sampleEvent = new SampleEvent("to be rescheduled");

        await broker.Publish(sampleEvent, TestContext.Current.CancellationToken);

        await receiver.WaitForEventsAsync(1, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var receivedEvents = receiver.GetReceivedEvents();
        var receivedEvent = Assert.Single(receivedEvents);
        Assert.Equal(sampleEvent, receivedEvent.Event);
        Assert.Equal(EventStatus.InProgress, receivedEvent.EventRecord.Status);
        // give room to maintenance task to reschedule the event
        await Task.Delay(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        await AssertScheduledStatusAsync(receivedEvent.EventRecord);
    }

    private async Task AssertScheduledStatusAsync(EventRecord eventRecord)
    {
        using var connection = new SqliteConnection(_connectionString);
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT status FROM {nameof(RescheduleTest)} WHERE id = @id";
        command.Parameters.AddWithValue("@id", long.Parse(eventRecord.Id));
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var status = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        Assert.Equal(EventStatus.Scheduled, (EventStatus)(long)status!);
    }

    public void Dispose()
    {
        _scope.ServiceProvider.GetRequiredService<IEventBroker>().Shutdown();
        _scope.Dispose();
        _serviceProvider.Dispose();
    }
}
