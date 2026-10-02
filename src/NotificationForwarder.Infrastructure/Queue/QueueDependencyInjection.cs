using NotificationForwarder.Infrastructure.Queue.Services.Implementation;

namespace NotificationForwarder.Infrastructure.Queue;

public static class QueueDependencyInjection
{
    public static IServiceCollection AddQueue(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<QueueOptions>()
            .Bind(configuration.GetSection(QueueOptions.SectionName))
            .Validate(options => options.Capacity is >= 1 and <= 1_000_000, "Queue:Capacity must be between 1 and 1000000.")
            .ValidateOnStart();
        return services.AddSingleton<INotificationBacklog, ChannelNotificationBacklog>();
    }
}
