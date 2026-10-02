namespace NotificationForwarder.Application.Notifications.ReceiveNotification;

public interface IReceiveNotificationCommandHandler
{
    Task<Result<ReceiveNotificationResult>> ReceiveAsync(ReceiveNotificationCommand command, CancellationToken cancellationToken = default);
}
