var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("redis")
    .WithRedisCommander();

var generator = builder.AddProject<Projects.CompanyEmployees_Generator>("generator")
    .WithReference(redis)
    .WithExternalHttpEndpoints();

builder.AddProject<Projects.Client_Wasm>("client")
    .WithReference(generator);

builder.Build().Run();