namespace NotificationForwarder.Application.Notifications.Common.Services;

/// <summary>Determines what kind of problem a notification describes and writes the alert text.</summary>
public interface IAlertGenerator
{
    /// <summary>Throws <see cref="AlertGenerationException"/> when the alert could not be produced.</summary>
    Task<GeneratedAlert> GenerateAsync(NotificationEntity notification, CancellationToken cancellationToken);
}

/// <summary>An expected provider failure: HTTP error, timeout, refusal or output that breaks the contract. Never carries provider bodies.</summary>
public sealed class AlertGenerationException : Exception
{
    public AlertGenerationException()
    {
    }

    public AlertGenerationException(string message) : base(message)
    {
    }

    public AlertGenerationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
