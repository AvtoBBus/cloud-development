var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("redis")
    .WithRedisCommander();

var minioAccessKey = builder.Configuration["MinIO:AccessKey"]!;
var minioSecretKey = builder.Configuration["MinIO:SecretKey"]!;

var minio = builder.AddMinioContainer("minio")
    .WithEnvironment("MINIO_ROOT_USER", minioAccessKey)
    .WithEnvironment("MINIO_ROOT_PASSWORD", minioSecretKey)
    //.WithBindMount("minio-data", "/data")
    .WaitFor(redis);

minio.WithEndpoint("http", endpoint => endpoint.Port = 9000);
minio.WithEndpoint("console", endpoint => endpoint.Port = 9001);

var sqs = builder.AddContainer("elasticmq", "softwaremill/elasticmq-native")
    .WithHttpEndpoint(targetPort: 9324, name: "http")
    .WithHttpHealthCheck("/?Action=ListQueues", endpointName: "http");

var gatewayPort = builder.Configuration.GetValue<int>("GatewayPort");
var gateway = builder
    .AddProject<Projects.CompanyEmployees_ApiGateway>("companyemployees-apigateway")
    .WithExternalHttpEndpoints();

for (var i = 0; i < 3; ++i)
{
    var currGenerator = builder.AddProject<Projects.CompanyEmployees_Generator>($"generator-{i + 1}")
        .WithEndpoint("http", endpoint => endpoint.Port = gatewayPort + 1 + i)
        .WithReference(redis)
        .WaitFor(redis)
        .WithEnvironment("Sqs__ServiceUrl", sqs.GetEndpoint("http"))
        .WaitFor(sqs);

    gateway
        .WithReference(currGenerator)
        .WaitFor(currGenerator);
}

var fileService = builder.AddProject<Projects.CompanyEmployees_FileService>("company-employee-fileservice")
    .WithEnvironment("Sqs__ServiceUrl", sqs.GetEndpoint("http"))
    .WithEnvironment("MinIO__BucketName", "company-employee")
    .WithEnvironment("BucketName", "company-employee")
    .WithReference(minio)
    .WaitFor(sqs)
    .WaitFor(minio);

builder.AddProject<Projects.Client_Wasm>("client")
        .WithReference(gateway)
        .WaitFor(gateway);

builder.Build().Run();