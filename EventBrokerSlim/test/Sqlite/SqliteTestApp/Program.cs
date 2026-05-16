using System.Text.Json;
using FuncPipeline;
using M.EventBrokerSlim;
using M.EventBrokerSlim.DependencyInjection;
using M.EventBrokerSlim.Persistent;
using M.EventBrokerSlim.PersistentEvents.Sqlite;
using SqliteTestApp;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<EventBroadcaster>();

// Register EventBroker with SQLite persistence
builder.Services.AddEventBroker(b => b
    .WithMaxConcurrentHandlers(4)
    .WithSqlitePersistence((db, broker) =>
    {
        db.ConnectionString =
            builder.Configuration.GetConnectionString("Sqlite")
            ?? throw new InvalidOperationException("Sqlite connection string is not configured.");
        db.CreateEventsTable();

        broker.PollingInterval = TimeSpan.FromSeconds(10);
    }));

// Register the event registry (maps event types to stable string names)
var eventRegistry = new EventRegistry()
    .Add<SampleEvent>("sample-event");

builder.Services.AddSingleton(eventRegistry);

var pipelineBuilder = PipelineBuilder
    .Create()
    .NewPipeline()
    .Execute(async (SampleEvent @event, ILogger<Program> logger, INext next, IRetryPolicy retryPolicy, CancellationToken cancellationToken) =>
    {
        try
        {
            await next.RunAsync();
        }
        catch(Exception ex)
        {
            logger.LogError(ex, "Error handling SampleEvent: {Message}", @event.Message);
            retryPolicy.RetryAfter(TimeSpan.FromSeconds(5));
        }
    })
    .Execute(async (SampleEvent @event, EventBroadcaster broadcaster, CancellationToken cancellationToken) =>
    {
        var handledAt = DateTimeOffset.UtcNow;
        broadcaster.Broadcast(new ProcessedEvent(@event.Message, @event.Timestamp, handledAt));
    })
    .Build();

// Register the event handler pipeline for SampleEvent with a handler name to enable persistence
builder.Services.AddEventHandlerPipeline<SampleEvent>(pipelineBuilder.Pipelines[0], opt => opt.WithHandlerName("sample-event-handler"));

var app = builder.Build();

// Start the persistent event broker (polling, maintenance loops)
app.Services.UsePersistentEventBroker(throwOnValidationErrors: true);

app.UseStaticFiles();

var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

// POST /api/publish — publish a SampleEvent
app.MapPost("/api/publish", async (IEventBroker eventBroker) =>
{
    var sampleEvent = new SampleEvent(DateTimeOffset.UtcNow, $"Hello at {DateTimeOffset.UtcNow:HH:mm:ss.fff}");
    await eventBroker.Publish(sampleEvent);
    return Results.Ok(new { sampleEvent.Timestamp, sampleEvent.Message });
});

// GET /api/events/stream — SSE endpoint
app.MapGet("/api/events/stream", async (EventBroadcaster broadcaster, HttpContext httpContext, CancellationToken ct) =>
{
    httpContext.Response.Headers.ContentType = "text/event-stream";
    httpContext.Response.Headers.CacheControl = "no-cache";
    httpContext.Response.Headers.Connection = "keep-alive";

    var subscription = broadcaster.Subscribe(out var reader);
    await using var _ = new AsyncDisposableWrapper(subscription);

    try
    {
        await foreach(var processedEvent in reader.ReadAllAsync(ct))
        {
            var json = JsonSerializer.Serialize(processedEvent, jsonOptions);
            await httpContext.Response.WriteAsync($"data: {json}\n\n", ct);
            await httpContext.Response.Body.FlushAsync(ct);
        }
    }
    catch(OperationCanceledException)
    {
        // Client disconnected
    }
});

// Fallback to index.html for SPA-like behavior
app.MapFallbackToFile("index.html");

app.Run();

// Helper to use IDisposable with await using
file sealed class AsyncDisposableWrapper(IDisposable disposable) : IAsyncDisposable
{
    public ValueTask DisposeAsync()
    {
        disposable.Dispose();
        return ValueTask.CompletedTask;
    }
}
