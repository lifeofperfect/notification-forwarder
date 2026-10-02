using NotificationForwarder.Domain.RateLimiting;

namespace NotificationForwarder.Application.Notifications.Common.Services.Implementation;

/// <summary>
/// Lets at most ten sends through in any rolling minute. Remembers when each recent send happened and makes the
/// eleventh wait until the oldest leaves the window. Ages are measured with the monotonic timestamp of
/// <see cref="TimeProvider"/>, so an adjustment of the wall clock can neither unlock nor lock capacity, and tests
/// advance a fake clock.
/// </summary>
public sealed class RollingSendGate(TimeProvider clock)
{
    private readonly Queue<long> _sends = new();
    private readonly Lock _gate = new();

    /// <summary>Takes a turn if one is free. Returns zero on success, otherwise the time until the next turn.</summary>
    public TimeSpan TryTakeTurn()
    {
        lock (_gate)
        {
            var now = clock.GetTimestamp();
            while (_sends.Count > 0 && clock.GetElapsedTime(_sends.Peek(), now) >= DeliveryRateLimit.Window)
            {
                _sends.Dequeue();
            }

            var delay = DeliveryRateLimit.GetDelay(_sends.Select(sentAt => clock.GetElapsedTime(sentAt, now)));
            if (delay == TimeSpan.Zero)
            {
                _sends.Enqueue(now);
            }

            return delay;
        }
    }

    /// <summary>Completes once a send is permitted. The turn is consumed on return.</summary>
    public async Task WaitForTurnAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var delay = TryTakeTurn();
            if (delay == TimeSpan.Zero)
            {
                return;
            }

            await Task.Delay(delay, clock, cancellationToken);
        }
    }
}
