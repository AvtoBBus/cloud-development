var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("redis")
    .WithRedisCommander();


var generator1 = builder.AddProject<Projects.CompanyEmployees_Generator>("generator-1")
    .WithEndpoint("http", endpoint => endpoint.Port = 5201)
    .WithReference(redis)
    .WaitFor(redis);

var generator2 = builder.AddProject<Projects.CompanyEmployees_Generator>("generator-2")
    .WithEndpoint("http", endpoint => endpoint.Port = 5202)
    .WithReference(redis)
    .WaitFor(redis);

var generator3 = builder.AddProject<Projects.CompanyEmployees_Generator>("generator-3")
    .WithEndpoint("http", endpoint => endpoint.Port = 5203)
    .WithReference(redis)
    .WaitFor(redis);

var gateway = builder.AddProject<Projects.CompanyEmployees_ApiGateway>("companyemployees-apigateway")
    .WithReference(generator1)
    .WithReference(generator2)
    .WithReference(generator3)
    .WithExternalHttpEndpoints()
    .WaitFor(generator1)
    .WaitFor(generator2)
    .WaitFor(generator3);

builder.AddProject<Projects.Client_Wasm>("client")
        .WithReference(gateway)
        .WaitFor(gateway);

builder.Build().Run();