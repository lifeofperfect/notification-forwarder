namespace NotificationForwarder.Api.Notifications.ReceiveNotification.V1;

/// <param name="Id">Identifier of the queued notification; it appears in the Discord message so alerts can be correlated.</param>
public sealed record ReceiveNotificationResponse(Guid Id);
