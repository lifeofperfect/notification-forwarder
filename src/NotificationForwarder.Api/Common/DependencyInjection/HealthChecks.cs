using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace NotificationForwarder.Api.Common.DependencyInjection;

public static class HealthChecks
{
    public static IServiceCollection AddApiHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks();
        return services;
    }

    public static IEndpointRouteBuilder MapApiHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        endpoints.MapHealthChecks("/health/ready");
        return endpoints;
    }
}
