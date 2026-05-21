using System;
using LiteDB;

namespace M.EventBrokerSlim.PersistentEvents.LiteDb.Internal;

internal class LiteDbInstanceWrapper : IDisposable
{
    private bool _dispose;

    public LiteDbInstanceWrapper(LiteDatabase liteDb)
    {
        LiteDb = liteDb;
    }

    public LiteDbInstanceWrapper(string connectionString)
    {
        LiteDb = new LiteDatabase(connectionString);
        _dispose = true;
    }

    public LiteDatabase LiteDb { get; }

    public void Dispose()
    {
        if (_dispose)
        {
            LiteDb.Dispose();
        }
    }   
}
