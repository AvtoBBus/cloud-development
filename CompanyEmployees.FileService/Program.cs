using Amazon.Runtime;
using Amazon.S3;
using Amazon.SQS;
using CompanyEmployees.FileService.Configuration;
using CompanyEmployees.FileService.Services;
using CompanyEmployees.ServiceDefaults;
using MassTransit;
using MassTransit.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var minioSettings = builder.Configuration.GetSection("MinIO");

var minioConfiguration = minioSettings.Get<MinioConfiguration>();
var minioAccessKey = minioConfiguration?.AccessKey;
var minioSecretKey = minioConfiguration?.SecretKey;
var minioEndpoint = minioConfiguration?.Endpoint;

builder.Services.Configure<MinioConfiguration>(minioSettings);

builder.Services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(
    new BasicAWSCredentials(minioAccessKey, minioSecretKey),
    new AmazonS3Config
    {
        ServiceURL = minioEndpoint,
        ForcePathStyle = true,
        AuthenticationRegion = "us-east-1"
    }
));

var sqsServiceUrl = builder.Configuration["SQS:ServiceUrl"] ?? "http://localhost:9324";

builder.Services.AddHostedService<SqsService>();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<EmployeeConsumer>();

    x.UsingAmazonSqs((context, cfg) =>
    {
        cfg.Host("us-east-1", h =>
        {
            h.AccessKey("test");
            h.SecretKey("test");
            h.Config(new AmazonSQSConfig
            {
                ServiceURL = sqsServiceUrl,
                AuthenticationRegion = "us-east-1"
            });
        });
        cfg.UseRawJsonSerializer((RawJsonSerializerOptions)RawSerializerOptions.AnyMessageType);

        cfg.ReceiveEndpoint("employees", e =>
        {
            e.ConfigureConsumeTopology = false;
            e.Consumer<EmployeeConsumer>(context);
        });
    });
});

builder.Services.AddHostedService<MinioInitializer>();

var app = builder.Build();

await app.RunAsync();