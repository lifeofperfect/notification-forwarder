using NotificationForwarder.Application.Notifications.Common.Services;
using NotificationForwarder.Application.Notifications.Common.Services.Implementation;

namespace NotificationForwarder.Application.Notifications.ProcessPendingNotification;

public sealed class ProcessPendingNotificationCommandHandler(
    IAlertGenerator generator,
    RollingSendGate sendGate,
    IAlertSender sender) : IProcessPendingNotificationCommandHandler
{
    public async Task ProcessAsync(NotificationEntity notification, CancellationToken cancellationToken = default)
    {
        var alert = await generator.GenerateAsync(notification, cancellationToken);
        await sendGate.WaitForTurnAsync(cancellationToken);
        await sender.SendAsync(notification, alert, cancellationToken);
    }
}

