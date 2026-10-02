namespace NotificationForwarder.Application.Notifications.Common.Services;

/// <summary>Delivers a generated alert to the external interface (Discord in production).</summary>
public interface IAlertSender
{
    /// <summary>Throws <see cref="AlertDeliveryException"/> when the external interface did not accept the alert.</summary>
    Task SendAsync(NotificationEntity notification, GeneratedAlert alert, CancellationToken cancellationToken);
}

/// <summary>An expected delivery failure: the channel rejected the message, timed out or could not be reached.</summary>
public sealed class AlertDeliveryException : Exception
{
    public AlertDeliveryException()
    {
    }

    public AlertDeliveryException(string message) : base(message)
    {
    }

    public AlertDeliveryException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public AlertDeliveryException(string message, int? statusCode) : base(message) => StatusCode = statusCode;

    /// <summary>HTTP status returned by the channel, when there was one.</summary>
    public int? StatusCode { get; }
}
