var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("events-db");
var db = postgres.AddDatabase("events", "events");

builder.AddProject<Projects.PostgreSqlTestApp>("postgresqltestapp")
    .WithReference(db)
    .WaitFor(db)
    .WithEnvironment("ConnectionStrings__PostgreSql", db);

try
{
    await builder.Build().RunAsync();
}
catch(TaskCanceledException)
{
    // Ignore cancellation during shutdown
}
