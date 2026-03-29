var builder = DistributedApplication.CreateBuilder(args);

var db = builder
    .AddPostgres("events-db")
    .AddDatabase("events", "events");

builder.AddProject<Projects.PostgreSqlTestApp>("postgresqltestapp")
    .WaitFor(db)
    .WithEnvironment("ConnectionStrings__PostgreSql", db);

builder.Build().Run();
