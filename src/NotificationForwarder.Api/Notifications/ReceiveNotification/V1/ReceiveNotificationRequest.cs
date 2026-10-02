namespace NotificationForwarder.Api.Notifications.ReceiveNotification.V1;

/// <summary>Inbound payload. Additional properties are ignored. A missing or null body is rejected by the framework with 400.</summary>
/// <param name="Level">trace, debug, info, warning, error or critical (case-insensitive).</param>
/// <param name="Message">The notification text, at most 2000 characters.</param>
/// <param name="Source">Optional name of the emitting system, at most 100 characters.</param>
public sealed record ReceiveNotificationRequest(string? Level, string? Message, string? Source = null);
