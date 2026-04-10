using FuncPipeline;
using M.EventBrokerSlim.DependencyInjection;
using M.EventBrokerSlim.Persistent;
using Microsoft.Extensions.DependencyInjection;
using PostgreSqlIntegrationTests;

namespace M.EventBrokerSlim.PersistentEvents.PostgreSql.Tests.Tests;

public class SingleEventTest : IDisposable
{
    private readonly Setup _setup;
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScope _scope;

    public SingleEventTest(Setup setup)
    {
        _setup = setup;
        var services = new ServiceCollection().AddEventBroker(_setup);

        var builder = PipelineBuilder
            .Create()
            .NewPipeline()
            .Execute(async (SampleEvent e, EventReceiver r, EventRecord record) => r.Add(record))
            .Build();
        services.AddEventHandlerPipeline<SampleEvent>(builder.Pipelines[0], o => o.WithHandlerName("sample-event-handler"));

        _serviceProvider = services.BuildServiceProvider();
        _serviceProvider.UsePersistentEventBroker(throwOnValidationErrors: true);
        _scope = _serviceProvider.CreateScope();
    }

    [Fact]
    public async Task Event_sent_and_received()
    {
        var broker = _scope.ServiceProvider.GetRequiredService<IEventBroker>();
        var receiver = _scope.ServiceProvider.GetRequiredService<EventReceiver>();
        var sampleEvent = new SampleEvent("hello from postgres handler!");

        await broker.Publish(sampleEvent, TestContext.Current.CancellationToken);

        var received = await receiver.WaitForSingleAsync<SampleEvent>(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(sampleEvent, received);
    }

    public void Dispose()
    {
        _scope.ServiceProvider.GetRequiredService<IEventBroker>().Shutdown();
        _scope.Dispose();
        _serviceProvider.Dispose();
    }
}
