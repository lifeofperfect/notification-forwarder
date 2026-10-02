namespace NotificationForwarder.Application.Notifications.ReceiveNotification;

/// <summary>Raw intake values. Validation and parsing happen in the handler, not at the HTTP boundary.</summary>
public sealed record ReceiveNotificationCommand(string? Level, string? Message, string? Source);
