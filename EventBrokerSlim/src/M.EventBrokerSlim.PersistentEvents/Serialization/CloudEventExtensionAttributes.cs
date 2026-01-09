using CloudNative.CloudEvents;

namespace M.EventBrokerSlim.PersistentEvents.Serialization;

public static class CloudEventExtensionAttributes
{
    public const string Retry = "retry";
    public const string Handler = "handler";
    public static readonly CloudEventAttribute[] All = [
            CloudEventAttribute.CreateExtension(Retry, CloudEventAttributeType.Integer),
            CloudEventAttribute.CreateExtension(Handler, CloudEventAttributeType.String)
        ];
}
