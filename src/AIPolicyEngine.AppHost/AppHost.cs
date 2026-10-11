var builder = DistributedApplication.CreateBuilder(args);
var redis = builder.AddRedis("redis");
var postgres = builder.AddPostgres("postgres");
var database = postgres.AddDatabase("aipolicy");
builder.AddProject<Projects.AIPolicyEngine_Api>("aipolicyengine-api")
    .WithReference(redis).WaitFor(redis)
    .WithReference(database).WaitFor(database)
    .WithExternalHttpEndpoints();
builder.Build().Run();
