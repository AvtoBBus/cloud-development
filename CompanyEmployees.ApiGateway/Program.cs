using CompanyEmployees.ApiGateway.LoadBalancer;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

var generators = builder.Configuration.GetSection("Generators").Get<string[]>() ?? [];

var addressOverrides = new List<KeyValuePair<string, string?>>();

for (var i = 0; i < generators.Length; ++i)
{
    var name = generators[i];
    var url = builder.Configuration[$"services:{name}:http:0"];

    if (!string.IsNullOrEmpty(url) && Uri.TryCreate(url, UriKind.Absolute, out var uri))
    {
        addressOverrides.Add(new($"Routes:0:DownstreamHostAndPorts:{i}:Host", uri.Host));
        addressOverrides.Add(new($"Routes:0:DownstreamHostAndPorts:{i}:Port", uri.Port.ToString()));
    }
}

if (addressOverrides.Count > 0)
    builder.Configuration.AddInMemoryCollection(addressOverrides);

builder.Services
    .AddOcelot(builder.Configuration)
    .AddCustomLoadBalancer((route, serviceDiscovery) =>
        new QueryBased(serviceDiscovery));

var app = builder.Build();

await app.UseOcelot();
await app.RunAsync();