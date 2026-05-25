var builder = DistributedApplication.CreateBuilder(args);

var mongodb = builder.AddMongoDB("events-db");

builder.AddProject<Projects.MongoDbTestApp>("mongodbtestapp")
    .WithReference(mongodb)
    .WaitFor(mongodb)
    .WithEnvironment("ConnectionStrings__MongoDb", mongodb);

try
{
    await builder.Build().RunAsync();
}
catch(TaskCanceledException)
{
    // Ignore cancellation during shutdown
}
