namespace NotificationForwarder.Application.Notifications.Common.Services;

/// <summary>Hand-off between HTTP intake and the background processor.</summary>
public interface INotificationBacklog
{
    /// <summary>Returns false when the queue is full; the caller should apply back-pressure.</summary>
    bool TryEnqueue(NotificationEntity notification);

    IAsyncEnumerable<NotificationEntity> ReadAllAsync(CancellationToken cancellationToken);
}
