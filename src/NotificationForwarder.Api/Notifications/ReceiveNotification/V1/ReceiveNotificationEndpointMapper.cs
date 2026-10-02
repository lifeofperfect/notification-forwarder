namespace NotificationForwarder.Api.Notifications.ReceiveNotification.V1;

internal static class ReceiveNotificationEndpointMapper
{
    public static ReceiveNotificationCommand ToCommand(this ReceiveNotificationRequest request) =>
        new(request.Level, request.Message, request.Source);

    public static ReceiveNotificationResponse ToResponse(this ReceiveNotificationResult result) =>
        new(result.NotificationId ?? throw new InvalidOperationException("Only an accepted notification has a response body."));
}
