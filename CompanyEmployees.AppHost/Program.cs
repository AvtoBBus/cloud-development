var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("redis")
    .WithRedisCommander();

var gatewayPort = builder.Configuration.GetValue<int>("GatewayPort");
var gateway = builder.AddProject<Projects.CompanyEmployees_ApiGateway>("companyemployees-apigateway");

for (var i = 0; i < 3; ++i)
{
    var currGenerator = builder.AddProject<Projects.CompanyEmployees_Generator>($"generator-{i + 1}")
        .WithEndpoint("http", endpoint => endpoint.Port = gatewayPort + 1 + i)
        .WithReference(redis)
        .WaitFor(redis);

    gateway
        .WithReference(currGenerator)
        .WithExternalHttpEndpoints()
        .WaitFor(currGenerator);
}

builder.AddProject<Projects.Client_Wasm>("client")
        .WithReference(gateway)
        .WaitFor(gateway);

builder.Build().Run();