namespace NotificationForwarder.Domain.Notifications;

/// <summary>A notification that qualifies for forwarding.</summary>
public sealed class NotificationEntity
{
    public const int MaximumSourceLength = 100;

    private NotificationEntity(Guid id, NotificationLevel level, NotificationMessage message, string? source, DateTimeOffset receivedAt)
    {
        Id = id;
        Level = level;
        Message = message;
        Source = source;
        ReceivedAt = receivedAt;
    }

    public Guid Id { get; }

    public NotificationLevel Level { get; }

    public NotificationMessage Message { get; }

    /// <summary>The system that emitted the notification, when the caller supplied one. One line.</summary>
    public string? Source { get; }

    public DateTimeOffset ReceivedAt { get; }

    /// <summary>Returns a validation message for the source, or null when it is acceptable.</summary>
    public static string? ValidateSource(string? source)
    {
        if (source is { Length: > MaximumSourceLength })
        {
            return $"The source must not exceed {MaximumSourceLength} characters.";
        }

        return source is not null && source.Any(char.IsControl)
            ? "The source must be a single line without control characters."
            : null;
    }

    public static NotificationEntity Receive(NotificationLevel level, NotificationMessage message, string? source, DateTimeOffset receivedAt)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!level.ShouldForward())
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "Only warning or higher notifications are forwarded.");
        }

        if (ValidateSource(source) is { } error)
        {
            throw new ArgumentException(error, nameof(source));
        }

        return new NotificationEntity(Guid.CreateVersion7(receivedAt), level, message, source?.Trim(), receivedAt);
    }
}
