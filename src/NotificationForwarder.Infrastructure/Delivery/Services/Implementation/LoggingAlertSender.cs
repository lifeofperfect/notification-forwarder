namespace NotificationForwarder.Infrastructure.Delivery.Services.Implementation;

/// <summary>Stand-in used when Discord is disabled: the alert is written to the log so the pipeline can be exercised locally.</summary>
public sealed partial class LoggingAlertSender(ILogger<LoggingAlertSender> logger) : IAlertSender
{
    public Task SendAsync(NotificationEntity notification, GeneratedAlert alert, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(alert);
        LogAlert(notification.Id, alert.Kind, notification.Level, alert.Title, alert.Message);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Discord is disabled. Alert for {NotificationId} [{Kind}] {Level}: {Title} | {Message}")]
    private partial void LogAlert(Guid notificationId, string kind, NotificationLevel level, string title, string message);
}
