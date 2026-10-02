using NotificationForwarder.Infrastructure.Delivery;
using NotificationForwarder.Infrastructure.Generation;
using NotificationForwarder.Infrastructure.Processing;
using NotificationForwarder.Infrastructure.Queue;

namespace NotificationForwarder.Infrastructure;

public static class InfrastructureDependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration) =>
        services
            .AddQueue(configuration)
            .AddGeneration(configuration)
            .AddDelivery(configuration)
            .AddProcessing();
}
