var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("events-db"); // .WithRedisInsight();

builder.AddProject<Projects.RedisTestApp>("redistestapp")
    .WithReference(redis)
    .WaitFor(redis)
    .WithEnvironment("ConnectionStrings__Redis", redis);

try
{
    await builder.Build().RunAsync();
}
catch(TaskCanceledException)
{
    // Ignore cancellation during shutdown
}
