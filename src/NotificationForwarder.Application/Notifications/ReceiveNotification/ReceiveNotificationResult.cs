namespace NotificationForwarder.Application.Notifications.ReceiveNotification;

public enum ReceiveNotificationOutcome
{
    /// <summary>The notification was queued for forwarding.</summary>
    Accepted,

    /// <summary>The level is below the forwarding threshold; nothing else happens.</summary>
    Ignored
}

public sealed record ReceiveNotificationResult(Guid? NotificationId, ReceiveNotificationOutcome Outcome);
