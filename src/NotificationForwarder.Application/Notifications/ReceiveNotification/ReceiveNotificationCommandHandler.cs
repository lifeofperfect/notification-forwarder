using NotificationForwarder.Application.Notifications.Common.Services;

namespace NotificationForwarder.Application.Notifications.ReceiveNotification;

public sealed partial class ReceiveNotificationCommandHandler(
    ReceiveNotificationCommandValidator validator,
    INotificationBacklog queue,
    TimeProvider clock,
    ILogger<ReceiveNotificationCommandHandler> logger) : IReceiveNotificationCommandHandler
{
    public async Task<Result<ReceiveNotificationResult>> ReceiveAsync(ReceiveNotificationCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<ReceiveNotificationResult>.Invalid(validation.ToValidationErrors());
        }

        var level = NotificationLevels.Parse(command.Level)!.Value;
        if (!level.ShouldForward())
        {
            LogIgnored(level);
            return Result<ReceiveNotificationResult>.Success(new ReceiveNotificationResult(null, ReceiveNotificationOutcome.Ignored));
        }

        var notification = command.ToEntity(level, clock.GetUtcNow());
        if (!queue.TryEnqueue(notification))
        {
            LogQueueFull(notification.Id, level);
            return Result<ReceiveNotificationResult>.Unavailable("The forwarding queue is full. Retry later.");
        }

        LogAccepted(notification.Id, level);
        return Result<ReceiveNotificationResult>.Success(new ReceiveNotificationResult(notification.Id, ReceiveNotificationOutcome.Accepted));
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Ignored {Level} notification below the forwarding threshold.")]
    private partial void LogIgnored(NotificationLevel level);

    [LoggerMessage(Level = LogLevel.Information, Message = "Accepted {Level} notification {NotificationId} for forwarding.")]
    private partial void LogAccepted(Guid notificationId, NotificationLevel level);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rejected {Level} notification {NotificationId}: the forwarding queue is full.")]
    private partial void LogQueueFull(Guid notificationId, NotificationLevel level);
}
