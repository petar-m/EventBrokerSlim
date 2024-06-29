using System;
using Microsoft.Extensions.ObjectPool;

namespace M.EventBrokerSlim.Internal;

internal sealed class DelegateParameterArrayPooledObjectPolicy : IPooledObjectPolicy<object[]>
{
    public object[] Create()
    {
        return new object[16];
    }

    public bool Return(object[] obj)
    {
        Array.Clear(obj);
        return true;
    }
}
