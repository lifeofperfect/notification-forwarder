using NotificationForwarder.Application.Notifications;

namespace NotificationForwarder.Application;

public static class ApplicationDependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services) =>
        services.AddNotifications();
}
