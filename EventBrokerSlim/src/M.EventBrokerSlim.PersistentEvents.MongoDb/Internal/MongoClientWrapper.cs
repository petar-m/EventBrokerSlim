using System;
using MongoDB.Driver;

namespace M.EventBrokerSlim.PersistentEvents.MongoDb.Internal;

internal class MongoClientWrapper : IDisposable
{
    private readonly MongoClient? _ownedClient;

    public MongoClientWrapper(IMongoDatabase database)
    {
        Database = database;
    }

    public MongoClientWrapper(string connectionString, string databaseName)
    {
        _ownedClient = new MongoClient(connectionString);
        Database = _ownedClient.GetDatabase(databaseName);
    }

    public IMongoDatabase Database { get; }

    public void Dispose() => _ownedClient?.Dispose();
}
