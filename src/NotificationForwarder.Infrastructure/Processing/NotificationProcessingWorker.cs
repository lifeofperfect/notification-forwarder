using Microsoft.Extensions.Hosting;
using NotificationForwarder.Application.Notifications.ProcessPendingNotification;

namespace NotificationForwarder.Infrastructure.Processing;

/// <summary>
/// Single consumer of the backlog. Items are processed one at a time, in arrival order, so the outbound cap can never be raced.
/// A failure affects only the notification it belongs to.
/// </summary>
public sealed partial class NotificationProcessingWorker(
    INotificationBacklog backlog,
    IServiceScopeFactory scopeFactory,
    ILogger<NotificationProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted();
        try
        {
            await foreach (var notification in backlog.ReadAllAsync(stoppingToken))
            {
                await ProcessAsync(notification, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }

        LogStopped();
    }

    private async Task ProcessAsync(NotificationEntity notification, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<IProcessPendingNotificationCommandHandler>();
            await handler.ProcessAsync(notification, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (AlertDeliveryException exception)
        {
            LogDeliveryFailed(notification.Id, exception.StatusCode, exception.Message);
        }
#pragma warning disable CA1031 // The loop must survive any single notification; the failure is logged with its id.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            LogUnexpectedFailure(exception, notification.Id);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Notification processing worker started.")]
    private partial void LogStarted();

    [LoggerMessage(Level = LogLevel.Information, Message = "Notification processing worker stopped.")]
    private partial void LogStopped();

    [LoggerMessage(Level = LogLevel.Error, Message = "Delivery of notification {NotificationId} failed with status {StatusCode}: {Reason}")]
    private partial void LogDeliveryFailed(Guid notificationId, int? statusCode, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected failure while processing notification {NotificationId}.")]
    private partial void LogUnexpectedFailure(Exception exception, Guid notificationId);
}
