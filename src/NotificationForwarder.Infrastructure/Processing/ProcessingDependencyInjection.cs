namespace NotificationForwarder.Infrastructure.Processing;

public static class ProcessingDependencyInjection
{
    /// <summary>Registers the background loop that drains the backlog. It runs inside whichever host calls AddInfrastructure.</summary>
    public static IServiceCollection AddProcessing(this IServiceCollection services) =>
        services.AddHostedService<NotificationProcessingWorker>();
}
