using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using M.EventBrokerSlim.Persistent;
using Microsoft.Extensions.Logging;

namespace M.EventBrokerSlim.PersistentEvents.MongoDb.Internal;

internal static class EventSerializer
{
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        WriteIndented = false,
        IgnoreReadOnlyProperties = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static object? DeserializePayload(string eventRecordId, string? payload, string eventName, EventRegistry eventRegistry, ILogger logger)
    {
        if(payload is null)
        {
            logger.LogWarning("Event record with id {EventRecordId} and event name {EventName} has null payload.", eventRecordId, eventName);
            return null;
        }

        Type? eventType = eventRegistry.GetEventType(eventName);
        if(eventType is null)
        {
            logger.LogWarning("Event record with id {EventRecordId} has unknown event name {EventName}.", eventRecordId, eventName);
            return null;
        }

        try
        {
            object? deserializedEvent = JsonSerializer.Deserialize(payload, eventType, _jsonSerializerOptions);
            if(deserializedEvent is null)
            {
                logger.LogWarning("Deserialization of event record with id {EventRecordId} and event name {EventName} resulted in null.", eventRecordId, eventName);
            }

            return deserializedEvent;
        }
        catch(Exception ex)
        {
            logger.LogError(ex, "Failed to deserialize payload of event record with id {EventRecordId} and event name {EventName}.", eventRecordId, eventName);
        }

        return null;
    }

    public static string SerializePayload<TEvent>(TEvent @event)
    {
        return JsonSerializer.Serialize(@event, _jsonSerializerOptions);
    }
}
