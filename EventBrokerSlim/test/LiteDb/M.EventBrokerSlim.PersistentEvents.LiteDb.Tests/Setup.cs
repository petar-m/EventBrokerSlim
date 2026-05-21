using LiteDB;
using M.EventBrokerSlim.PersistentEvents.LiteDb.Tests;

[assembly: AssemblyFixture(typeof(Setup))]

namespace M.EventBrokerSlim.PersistentEvents.LiteDb.Tests;

public class Setup : IDisposable
{
    private readonly string _tempDirectory;

    public Setup()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"ebs_litedb_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
        var connection = $"Filename={Path.Combine(_tempDirectory, "events.db")};Connection=Direct;";
        Database = new LiteDatabase(connection);
    }

    public LiteDatabase Database { get; }

    public void Dispose()
    {
        Database.Dispose();
        try
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
