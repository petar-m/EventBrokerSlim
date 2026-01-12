using System;
using CloudNative.CloudEvents;

namespace M.EventBrokerSlim.PersistentEvents.Serialization;

public static class CloudEventsExtensionAttributes
{
    public const string RetryAttempt = "retryattempt";
    public const string LastRetryDelay = "lastretrydelayms";
    public const string Handler = "handler";

    public static readonly CloudEventAttribute[] All = [
            CloudEventAttribute.CreateExtension(RetryAttempt, CloudEventAttributeType.Integer),
            CloudEventAttribute.CreateExtension(LastRetryDelay, CloudEventAttributeType.Integer),
            CloudEventAttribute.CreateExtension(Handler, CloudEventAttributeType.String),
        ];

    public static void SetRetryAttempt(this CloudEvent cloudEvent, int attempt) => cloudEvent[RetryAttempt] = attempt;

    public static int GetRetryAttempt(this CloudEvent cloudEvent) => (int)(cloudEvent[RetryAttempt] ?? 0);

    public static void SetLastRetryDelay(this CloudEvent cloudEvent, TimeSpan delay) => cloudEvent[LastRetryDelay] = (int)delay.TotalMilliseconds;

    public static TimeSpan GetLastRetryDelay(this CloudEvent cloudEvent) => TimeSpan.FromMilliseconds((int)(cloudEvent[LastRetryDelay] ?? 0));

    public static void SetHandler(this CloudEvent cloudEvent, string handlerName) => cloudEvent[Handler] = handlerName;

    public static string? GetHandler(this CloudEvent cloudEvent) => (string?)cloudEvent[Handler];

}
