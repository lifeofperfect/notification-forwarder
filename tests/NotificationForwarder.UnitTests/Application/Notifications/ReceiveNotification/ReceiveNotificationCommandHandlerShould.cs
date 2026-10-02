using Ardalis.Result;

namespace NotificationForwarder.UnitTests.Application.Notifications.ReceiveNotification;

public sealed class ReceiveNotificationCommandHandlerShould
{
    private readonly INotificationBacklog _queue = Substitute.For<INotificationBacklog>();
    private readonly FakeTimeProvider _clock = new(NotificationBuilder.ReceivedAt);
    private readonly ReceiveNotificationCommandHandler _handler;

    public ReceiveNotificationCommandHandlerShould()
    {
        _queue.TryEnqueue(Arg.Any<NotificationEntity>()).Returns(true);
        _handler = new ReceiveNotificationCommandHandler(new ReceiveNotificationCommandValidator(), _queue, _clock,
            NullLogger<ReceiveNotificationCommandHandler>.Instance);
    }

    [Fact]
    public async Task IgnoreLevelsBelowWarningWithoutQueueing()
    {
        var result = await _handler.ReceiveAsync(new ReceiveNotificationCommand("info", "Nightly job finished", "billing"), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Outcome.ShouldBe(ReceiveNotificationOutcome.Ignored);
        result.Value.NotificationId.ShouldBeNull();
        _queue.DidNotReceiveWithAnyArgs().TryEnqueue(default!);
    }

    [Fact]
    public async Task QueueWarningOrHigherWithReceiptTime()
    {
        NotificationEntity? queued = null;
        _queue.TryEnqueue(Arg.Do<NotificationEntity>(notification => queued = notification)).Returns(true);

        var result = await _handler.ReceiveAsync(new ReceiveNotificationCommand("Error", "  DB timeout  ", "payments-api"), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Outcome.ShouldBe(ReceiveNotificationOutcome.Accepted);
        queued.ShouldNotBeNull();
        result.Value.NotificationId.ShouldBe(queued.Id);
        queued.Level.ShouldBe(NotificationLevel.Error);
        queued.Message.Value.ShouldBe("DB timeout");
        queued.Source.ShouldBe("payments-api");
        queued.ReceivedAt.ShouldBe(NotificationBuilder.ReceivedAt);
    }

    [Fact]
    public async Task ReportUnavailableWhenTheQueueIsFull()
    {
        _queue.TryEnqueue(Arg.Any<NotificationEntity>()).Returns(false);

        var result = await _handler.ReceiveAsync(new ReceiveNotificationCommand("critical", "Down", null), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ResultStatus.Unavailable);
    }
}
