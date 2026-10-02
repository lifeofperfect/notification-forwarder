namespace NotificationForwarder.Domain.Notifications;

/// <summary>
/// Severity of an incoming notification. The declaration order is the severity order:
/// anything at or above <see cref="Warning"/> is forwarded to the external channel.
/// </summary>
public enum NotificationLevel
{
    Trace,
    Debug,
    Info,
    Warning,
    Error,
    Critical
}

public static class NotificationLevels
{
    public static NotificationLevel? Parse(string? value) =>
        value?.Trim().ToUpperInvariant() switch
        {
            "TRACE" => NotificationLevel.Trace,
            "DEBUG" => NotificationLevel.Debug,
            "INFO" => NotificationLevel.Info,
            "WARN" or "WARNING" => NotificationLevel.Warning,
            "ERR" or "ERROR" => NotificationLevel.Error,
            "FATAL" or "CRITICAL" => NotificationLevel.Critical,
            _ => null
        };

    public static bool ShouldForward(this NotificationLevel level) => level >= NotificationLevel.Warning;
}
