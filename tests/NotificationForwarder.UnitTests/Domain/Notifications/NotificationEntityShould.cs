namespace NotificationForwarder.UnitTests.Domain.Notifications;

public sealed class NotificationEntityShould
{
    [Fact]
    public void RefuseLevelsBelowWarning() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new NotificationBuilder().WithLevel(NotificationLevel.Info).Build());
}
