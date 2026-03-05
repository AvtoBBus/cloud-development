using CompanyEmployees.Generator.Services;
using CompanyEmployees.ServiceDefaults;
using Serilog;

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