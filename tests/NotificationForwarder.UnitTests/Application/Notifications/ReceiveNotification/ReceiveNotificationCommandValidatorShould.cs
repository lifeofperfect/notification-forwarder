namespace NotificationForwarder.UnitTests.Application.Notifications.ReceiveNotification;

public sealed class ReceiveNotificationCommandValidatorShould
{
    private readonly ReceiveNotificationCommandValidator _validator = new();

    [Fact]
    public async Task ReportEveryInvalidField()
    {
        var result = await _validator.ValidateAsync(
            new ReceiveNotificationCommand("severe", " ", new string('s', NotificationEntity.MaximumSourceLength + 1)),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.PropertyName).ShouldBe(["Level", "Message", "Source"], ignoreOrder: true);
    }

    [Fact]
    public async Task RejectMessagesAboveTheLimit()
    {
        var result = await _validator.ValidateAsync(
            new ReceiveNotificationCommand("error", new string('m', NotificationMessage.MaximumLength + 1), null),
            TestContext.Current.CancellationToken);

        result.Errors.ShouldHaveSingleItem().PropertyName.ShouldBe("Message");
    }

    [Fact]
    public async Task RejectControlCharactersButAllowLineBreaksInTheMessage()
    {
        var accepted = await _validator.ValidateAsync(new ReceiveNotificationCommand("error", "line one\nline two\tend", "payments-api"), TestContext.Current.CancellationToken);
        var rejected = await _validator.ValidateAsync(new ReceiveNotificationCommand("error", "bad\u0001byte", "two\nlines"), TestContext.Current.CancellationToken);

        accepted.IsValid.ShouldBeTrue();
        rejected.Errors.Select(error => error.PropertyName).ShouldBe(["Message", "Source"], ignoreOrder: true);
    }
}
