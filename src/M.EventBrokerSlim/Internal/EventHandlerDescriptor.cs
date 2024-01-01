using System;
using System.Threading.Tasks;

namespace M.EventBrokerSlim.Internal;

internal sealed record EventHandlerDescriptor(
    string Key,
    Type EventType,
    Type InterfaceType,
    Func<object, object, Task> Handle,
    Func<object, object, Exception, Task> OnError);
