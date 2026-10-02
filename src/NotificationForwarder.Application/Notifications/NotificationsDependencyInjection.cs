using NotificationForwarder.Application.Notifications.Common.Services.Implementation;
using NotificationForwarder.Application.Notifications.ProcessPendingNotification;
using NotificationForwarder.Application.Notifications.ReceiveNotification;

namespace NotificationForwarder.Application.Notifications;

public static class NotificationsDependencyInjection
{
    public static IServiceCollection AddNotifications(this IServiceCollection services) =>
        services
            .AddReceiveNotification()
            .AddProcessPendingNotification();

    public static IServiceCollection AddReceiveNotification(this IServiceCollection services) =>
        services
            .AddScoped<ReceiveNotificationCommandValidator>()
            .AddScoped<IReceiveNotificationCommandHandler, ReceiveNotificationCommandHandler>();

    public static IServiceCollection AddProcessPendingNotification(this IServiceCollection services) =>
        services
            .AddSingleton<TemplateAlertGenerator>()
            .AddSingleton<RollingSendGate>()
            .AddScoped<IProcessPendingNotificationCommandHandler, ProcessPendingNotificationCommandHandler>();
}
