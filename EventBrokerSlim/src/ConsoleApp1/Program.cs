using CloudNative.CloudEvents.Extensions;
using FuncPipeline;
using M.EventBrokerSlim.DependencyInjection;
using M.EventBrokerSlim.PersistentEvents;
using M.EventBrokerSlim.PersistentEvents.Serialization;

var eventNameRegistry = new EventNameRegistry();
eventNameRegistry.Add<MyEvent>("my-event");

var pipeline = PipelineBuilder.Create().NewPipeline().Build();
var pipelineRegistry = new PipelineRegistry([new EventPipeline(typeof(MyEvent), pipeline.Pipelines[0], "my-handler") ], null!);

var serializer = new CloudEventSerializer(eventNameRegistry, pipelineRegistry);

var serializedEvents = serializer.Serialize(new MyEvent(true, "This is a test event."), 3);

foreach (var serializedEvent in serializedEvents)
    Console.WriteLine(serializedEvent);

var deserialized = serializer.Deserialize(serializedEvents[0]);

Type? et = eventNameRegistry.GetEventType(deserialized.Type);
var data = serializer.DeserializeData(deserialized, et!);
Console.WriteLine(data);

var h = pipelineRegistry.Get(deserialized[CloudEventExtensionAttributes.Handler].ToString());

Console.WriteLine(deserialized.Type);
Console.WriteLine(deserialized[CloudEventExtensionAttributes.Handler]);
Console.WriteLine(deserialized[CloudEventExtensionAttributes.Retry]);
public record MyEvent(bool IsReal, string Description);
