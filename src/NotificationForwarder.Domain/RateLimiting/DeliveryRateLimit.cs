namespace NotificationForwarder.Domain.RateLimiting;

/// <summary>
/// The outbound cap from the brief: at most <see cref="MaximumMessages"/> messages in any rolling <see cref="Window"/>.
/// Pure arithmetic over the ages of previous sends, so it can be unit tested without a clock.
/// </summary>
public static class DeliveryRateLimit
{
    public const int MaximumMessages = 10;

    public static readonly TimeSpan Window = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Given how long ago each previous send happened, returns how long a caller must wait before another send is allowed.
    /// Zero means a send is allowed immediately.
    /// </summary>
    public static TimeSpan GetDelay(IEnumerable<TimeSpan> sendAges)
    {
        ArgumentNullException.ThrowIfNull(sendAges);

        // A send still counts while it is younger than one window. Youngest first.
        var active = sendAges.Where(age => age < Window).Order().ToList();
        if (active.Count < MaximumMessages)
        {
            return TimeSpan.Zero;
        }

        // Capacity returns when only nine remain in the window, that is when the tenth-youngest send turns one window old.
        return Window - active[MaximumMessages - 1];
    }
}
