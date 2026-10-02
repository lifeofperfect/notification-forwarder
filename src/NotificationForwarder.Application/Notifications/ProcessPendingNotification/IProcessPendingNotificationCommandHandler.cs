namespace NotificationForwarder.Application.Notifications.ProcessPendingNotification;

public interface IProcessPendingNotificationCommandHandler
{
    /// <summary>
    /// Generates the alert for one queued notification and delivers it, honouring the outbound rate limit.
    /// Throws <see cref="Common.Services.AlertDeliveryException"/> when the channel did not accept the alert.
    /// </summary>
    Task ProcessAsync(NotificationEntity notification, CancellationToken cancellationToken = default);
}
