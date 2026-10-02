using System.Globalization;
using Ardalis.Result;
using Ardalis.Result.AspNetCore;

namespace NotificationForwarder.Api.Notifications.ReceiveNotification.V1;

/// <summary>Receives a notification. Warning or higher is queued for AI classification and forwarding to Discord.</summary>
[ApiController]
[Route(Routing.V1)]
[Produces("application/json")]
public sealed class ReceiveNotificationEndpoint(IReceiveNotificationCommandHandler handler) : ControllerBase
{
    private const int QueueFullRetrySeconds = 5;

    /// <summary>Receives one notification.</summary>
    /// <param name="request">The notification payload.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="202">The notification was queued for forwarding; the body carries its id.</response>
    /// <response code="204">The level is below warning; the notification was ignored.</response>
    /// <response code="400">The payload is invalid.</response>
    /// <response code="503">The forwarding queue is full; retry after the indicated delay.</response>
    [HttpPost("notifications")]
    [Consumes("application/json")]
    [ProducesResponseType<ReceiveNotificationResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ReceiveAsync([FromBody] ReceiveNotificationRequest request, CancellationToken cancellationToken)
    {
        var result = await handler.ReceiveAsync(request.ToCommand(), cancellationToken);
        if (!result.IsSuccess)
        {
            if (result.Status == ResultStatus.Unavailable)
            {
                Response.Headers.RetryAfter = QueueFullRetrySeconds.ToString(CultureInfo.InvariantCulture);
            }

            return result.ToActionResult(this).Result
                ?? throw new InvalidOperationException($"No HTTP response is defined for a {result.Status} result.");
        }

        return result.Value.Outcome == ReceiveNotificationOutcome.Accepted
            ? Accepted(result.Value.ToResponse())
            : NoContent();
    }
}
