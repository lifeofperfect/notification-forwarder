namespace NotificationForwarder.UnitTests.Domain.Notifications;

public sealed class NotificationLevelShould
{
    [Theory]
    [InlineData("info", NotificationLevel.Info)]
    [InlineData("WARNING", NotificationLevel.Warning)]
    [InlineData(" warning ", NotificationLevel.Warning)]
    [InlineData("warn", NotificationLevel.Warning)]
    [InlineData("err", NotificationLevel.Error)]
    [InlineData("fatal", NotificationLevel.Critical)]
    public void ParseKnownNamesCaseInsensitively(string input, NotificationLevel expected) =>
        NotificationLevels.Parse(input).ShouldBe(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("severe")]
    public void RejectUnknownNames(string? input) =>
        NotificationLevels.Parse(input).ShouldBeNull();

    [Theory]
    [InlineData(NotificationLevel.Info, false)]
    [InlineData(NotificationLevel.Warning, true)]
    [InlineData(NotificationLevel.Critical, true)]
    public void ForwardWarningAndAbove(NotificationLevel level, bool expected) =>
        level.ShouldForward().ShouldBe(expected);
}
