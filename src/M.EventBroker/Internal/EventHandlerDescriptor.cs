using System;
using System.Threading.Tasks;

namespace M.EventBrokerSlim.Internal;

internal record EventHandlerDescriptor(
    string Key,
    Type InterfaceType,
    Func<object, object, Task> Handle,
    Func<object, object, Task<bool>> ShouldHandle,
    Func<object, object, Exception, Task> OnError);
