using System;
using System.Collections.Immutable;
using System.Text.Json;
using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;
using M.EventBrokerSlim.DependencyInjection;

namespace M.EventBrokerSlim.PersistentEvents.Serialization;

public class CloudEventSerializer
{
    private static readonly JsonEventFormatter _jsonEventFormatter = new JsonEventFormatter();
    private readonly EventNameRegistry _eventNameRegistry;
    private readonly PipelineRegistry _pipelineRegistry;

    public CloudEventSerializer(EventNameRegistry eventNameRegistry, PipelineRegistry pipelineRegistry)
    {
        _eventNameRegistry = eventNameRegistry;
        _pipelineRegistry = pipelineRegistry;
    }

    public string[] Serialize<TEvent>(TEvent @event, int retry)
    {
        string? eventName = _eventNameRegistry.GetEventName<TEvent>();
        if(eventName is null)
        {
            throw new InvalidOperationException($"Event type '{typeof(TEvent).FullName}' is not registered in the EventNameRegistry.");
        }

        ImmutableArray<EventPipeline> handlers = _pipelineRegistry.Get(typeof(TEvent));
        if(handlers.IsEmpty)
        {
            // TODO: Log warning: No handlers registered for event type
        }

        var serializedEvents = new string[handlers.Length];
        for(int i = 0; i < handlers.Length; i++)
        {
            var cloudEvent = new CloudEvent(CloudEventExtensionAttributes.All)
            {
#if NET9_0_OR_GREATER
                Id = Guid.CreateVersion7().ToString(),
#else
                Id = Guid.NewGuid().ToString(),
#endif
                DataContentType = "application/json",
                Source = new Uri("urn:event-broker-slim"),
                Type = eventName,
                Data = @event,
                Time = DateTimeOffset.UtcNow,
            };

            cloudEvent["retry"] = 0;
            cloudEvent["handler"] = handlers[i].HandlerName;

            serializedEvents[i] = _jsonEventFormatter.ConvertToJsonElement(cloudEvent).ToString();
        }

        return serializedEvents;
    }

    public CloudEvent Deserialize(string serializedCloudEvent)
    {
        return _jsonEventFormatter.ConvertFromJsonElement(
            JsonDocument.Parse(serializedCloudEvent).RootElement,
            CloudEventExtensionAttributes.All);
    }

    public object DeserializeData(CloudEvent cloudEvent, Type dataType)
    {
        var jObject = (JsonElement)cloudEvent.Data;
        return jObject.Deserialize(dataType);
    }
}
