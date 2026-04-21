using CompanyEmployees.Generator.Services;
using CompanyEmployees.ServiceDefaults;
using Serilog;
using MassTransit;
using Amazon.SQS;
using LocalStack.Client.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddRedisDistributedCache("redis");

builder.Services.AddSingleton<CompanyEmployeeGenerator>();
builder.Services.AddScoped<CompanyEmployeeService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var sqsServiceUrl = builder.Configuration["Sqs:ServiceUrl"];
if (!string.IsNullOrEmpty(sqsServiceUrl))
{
    builder.Services.AddMassTransit(x =>
    {
        x.UsingAmazonSqs((_, cfg) =>
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
            cfg.UseRawJsonSerializer(RawSerializerOptions.AnyMessageType);
        });
    });

    builder.Services.AddScoped<IEmployeePublisher, EmployeePublisher>();
}

var app = builder.Build();

app.UseCors();
app.UseSerilogRequestLogging();

app.MapDefaultEndpoints();

app.MapGet("/employee", async (
    int id,
    CompanyEmployeeService service,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    logger.LogInformation("Received request for company employee with ID: {Id}", id);

    if (id <= 0)
    {
        logger.LogWarning("Received invalid ID: {Id}", id);
        return Results.BadRequest(new { error = "ID must be a positive number" });
    }

    try
    {
        var application = await service.GetByIdAsync(id, cancellationToken);

        return Results.Ok(application);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error while getting company employee {Id}", id);
        return Results.Problem("An error occurred while processing the request");
    }
})
.WithName("GetCompanyEmployee");

app.Run();