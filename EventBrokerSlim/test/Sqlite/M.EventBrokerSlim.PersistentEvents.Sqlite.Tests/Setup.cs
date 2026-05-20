using M.EventBrokerSlim.PersistentEvents.Sqlite.Tests;

[assembly: AssemblyFixture(typeof(Setup))]

namespace M.EventBrokerSlim.PersistentEvents.Sqlite.Tests;

public class Setup : IDisposable
{
    private readonly string _tempDirectory;

    public Setup()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"ebs_sqlite_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
    }

    public string GetConnectionString(string testName) =>
        $"Data Source={Path.Combine(_tempDirectory, $"{testName}.db")}";

    public string GetConnectionString() => $"Data Source={Path.Combine(_tempDirectory, "events.db")}";

    public void Dispose()
    {
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
