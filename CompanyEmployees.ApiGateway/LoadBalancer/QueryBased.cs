using Ocelot.LoadBalancer.Errors;
using Ocelot.LoadBalancer.Interfaces;
using Ocelot.Responses;
using Ocelot.ServiceDiscovery.Providers;
using Ocelot.Values;

namespace CompanyEmployees.ApiGateway.LoadBalancer;
public class QueryBased(IServiceDiscoveryProvider serviceDiscovery)
    : ILoadBalancer
{
    private const string IdQueryParamName = "id";

    public string Type => nameof(QueryBased);

    public async Task<Response<ServiceHostAndPort>> LeaseAsync(HttpContext httpContext)
    {
        var services = await serviceDiscovery.GetAsync();

        if (services is null)
            return new ErrorResponse<ServiceHostAndPort>(
                new ServicesAreNullError("Service discovery returned null"));

        if (services.Count == 0)
            return new ErrorResponse<ServiceHostAndPort>(
                new ServicesAreNullError("No downstream services are available"));

        var query = httpContext.Request.Query;

        if (!query.TryGetValue(IdQueryParamName, out var idValues) || idValues.Count <= 0)
        {
            return SelectRandomService(services);
        }

        var idStr = idValues.First();

        if (string.IsNullOrEmpty(idStr) || !int.TryParse(idStr, out var id) || id < 0)
        {
            return SelectRandomService(services);
        }

        return new OkResponse<ServiceHostAndPort>(services[id % services.Count].HostAndPort);
    }

    private OkResponse<ServiceHostAndPort> SelectRandomService(List<Service> currentServices)
    {
        var randomIndex = Random.Shared.Next(currentServices.Count);
        return new OkResponse<ServiceHostAndPort>(currentServices[randomIndex].HostAndPort);
    }
    public void Release(ServiceHostAndPort hostAndPort) { }
}