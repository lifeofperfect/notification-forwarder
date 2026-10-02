namespace NotificationForwarder.Application.Notifications.Common.Services.Implementation;

/// <summary>Wraps the AI generator so that any expected failure degrades to the template alert instead of losing the notification.</summary>
public sealed partial class FallbackAlertGenerator(
    IAlertGenerator primary,
    TemplateAlertGenerator fallback,
    ILogger<FallbackAlertGenerator> logger) : IAlertGenerator
{
    public async Task<GeneratedAlert> GenerateAsync(NotificationEntity notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        try
        {
            return await primary.GenerateAsync(notification, cancellationToken);
        }
        catch (AlertGenerationException exception)
        {
            LogFallback(notification.Id, exception.Message);
            return await fallback.GenerateAsync(notification, cancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI generation failed for notification {NotificationId} ({Reason}); using the template alert.")]
    private partial void LogFallback(Guid notificationId, string reason);
}
