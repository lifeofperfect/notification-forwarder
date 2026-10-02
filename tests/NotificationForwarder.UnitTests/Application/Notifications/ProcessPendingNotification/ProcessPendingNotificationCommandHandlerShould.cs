using NotificationForwarder.Application.Notifications.ProcessPendingNotification;

namespace NotificationForwarder.UnitTests.Application.Notifications.ProcessPendingNotification;

public sealed class ProcessPendingNotificationCommandHandlerShould
{
    private readonly IAlertGenerator _generator = Substitute.For<IAlertGenerator>();
    private readonly IAlertSender _sender = Substitute.For<IAlertSender>();
    private readonly FakeTimeProvider _clock = new(NotificationBuilder.ReceivedAt);
    private readonly ProcessPendingNotificationCommandHandler _handler;

    public ProcessPendingNotificationCommandHandlerShould()
    {
        _generator.GenerateAsync(Arg.Any<NotificationEntity>(), Arg.Any<CancellationToken>())
            .Returns(NotificationBuilder.Alert(message: "AI message"));
        _handler = new ProcessPendingNotificationCommandHandler(_generator, new RollingSendGate(_clock), _sender);
    }

    [Fact]
    public async Task GenerateThenSendTheGeneratedAlert()
    {
        var notification = new NotificationBuilder().Build();

        await _handler.ProcessAsync(notification, TestContext.Current.CancellationToken);

        Received.InOrder(() =>
        {
            _generator.GenerateAsync(notification, Arg.Any<CancellationToken>());
            _sender.SendAsync(notification, Arg.Is<GeneratedAlert>(alert => alert.Message == "AI message"), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task WaitForATurnBeforeTheEleventhSendInAMinute()
    {
        for (var i = 0; i < DeliveryRateLimit.MaximumMessages; i++)
        {
            await _handler.ProcessAsync(new NotificationBuilder().Build(), TestContext.Current.CancellationToken);
        }

        var eleventh = new NotificationBuilder().Build();
        var pending = _handler.ProcessAsync(eleventh, TestContext.Current.CancellationToken);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        pending.IsCompleted.ShouldBeFalse();
        await _sender.DidNotReceive().SendAsync(eleventh, Arg.Any<GeneratedAlert>(), Arg.Any<CancellationToken>());

        _clock.Advance(DeliveryRateLimit.Window);
        await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await _sender.Received(1).SendAsync(eleventh, Arg.Any<GeneratedAlert>(), Arg.Any<CancellationToken>());
    }
}
