var builder = DistributedApplication.CreateBuilder(args);

var dbServer = builder.AddSqlServer("events-db");
var db = dbServer.AddDatabase("events", "events");

builder.AddProject<Projects.SqlServerTestApp>("sqlservertestapp")
    .WithReference(db)
    .WaitFor(db)
    .WithEnvironment("ConnectionStrings__SqlServer", db);

try
{
    await builder.Build().RunAsync();
}
catch(TaskCanceledException)
{
    // Ignore cancellation during shutdown
}
