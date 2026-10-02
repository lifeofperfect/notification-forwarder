namespace NotificationForwarder.UnitTests.Domain.RateLimiting;

public sealed class DeliveryRateLimitShould
{
    private static IEnumerable<TimeSpan> Ages(int count, int oldestSeconds) =>
        Enumerable.Range(0, count).Select(index => TimeSpan.FromSeconds(oldestSeconds - index));

    [Fact]
    public void AllowSendingWhileFewerThanTenInTheWindow() =>
        DeliveryRateLimit.GetDelay(Ages(9, oldestSeconds: 8)).ShouldBe(TimeSpan.Zero);

    [Fact]
    public void WaitForTheOldestOfTenToExpire()
    {
        // Ten sends aged 9 s down to 0 s; the one aged 9 s leaves the window 51 s from now.
        DeliveryRateLimit.GetDelay(Ages(10, oldestSeconds: 9)).ShouldBe(TimeSpan.FromSeconds(51));
    }

    [Fact]
    public void IgnoreSendsOlderThanTheWindow() =>
        DeliveryRateLimit.GetDelay(Ages(10, oldestSeconds: 69)).ShouldBe(TimeSpan.Zero);
}
