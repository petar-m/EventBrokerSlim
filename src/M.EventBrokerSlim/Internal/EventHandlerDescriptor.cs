using System;
using System.Threading;
using System.Threading.Tasks;

namespace M.EventBrokerSlim.Internal;

internal sealed record EventHandlerDescriptor(
    Guid Key,
    Type EventType,
    Type InterfaceType,
    Func<object, object, CancellationToken, Task> Handle,
    Func<object, object, Exception, CancellationToken, Task> OnError);
