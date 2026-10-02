using System.Net;
using System.Net.Http.Json;

namespace NotificationForwarder.Infrastructure.Delivery.Services.Implementation;

/// <summary>Posts the alert to a Discord webhook. Any rejection, timeout or transport error becomes an <see cref="AlertDeliveryException"/>.</summary>
public sealed partial class DiscordWebhookAlertSender(
    HttpClient httpClient,
    IOptions<DiscordOptions> options,
    TimeProvider clock,
    ILogger<DiscordWebhookAlertSender> logger) : IAlertSender
{
    public async Task SendAsync(NotificationEntity notification, GeneratedAlert alert, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(alert);
        var settings = options.Value;
        using var timeout = new CancellationTokenSource(settings.RequestTimeout, clock);
        using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        HttpStatusCode status;
        try
        {
            using var response = await httpClient.PostAsJsonAsync(WithWait(settings.WebhookUrl!), notification.ToPayload(alert, settings.Username), requestCancellation.Token);
            status = response.StatusCode;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AlertDeliveryException($"Discord did not answer within {settings.RequestTimeout.TotalSeconds:0} seconds.", exception);
        }
        catch (HttpRequestException exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new AlertDeliveryException($"The Discord request failed in transport ({exception.HttpRequestError}).", exception);
        }

        if (status == HttpStatusCode.TooManyRequests)
        {
            throw new AlertDeliveryException("Discord rate-limited the webhook.", (int)status);
        }

        if ((int)status is < 200 or > 299)
        {
            throw new AlertDeliveryException($"Discord rejected the message with HTTP {(int)status}.", (int)status);
        }

        LogSent(notification.Id, notification.Level, alert.Kind);
    }

    /// <summary>With wait=true Discord confirms the post with 200 and the created message instead of a bare 204.</summary>
    private static Uri WithWait(Uri webhook) =>
        new(webhook.AbsoluteUri + (string.IsNullOrEmpty(webhook.Query) ? "?wait=true" : "&wait=true"));

    [LoggerMessage(Level = LogLevel.Information, Message = "Forwarded {Level} notification {NotificationId} to Discord: {Kind}.")]
    private partial void LogSent(Guid notificationId, NotificationLevel level, string kind);
}
