using CompanyEmployees.ApiGateway.LoadBalancer;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("ocelot.json", optional: false, reloadOnChange: true)
    .AddOcelot();

builder.Services
    .AddOcelot(builder.Configuration)
    .AddCustomLoadBalancer((route, serviceDiscovery) =>
        new QueryBased(serviceDiscovery));

var app = builder.Build();

await app.UseOcelot();
await app.RunAsync();