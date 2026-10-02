using NotificationForwarder.Domain.RateLimiting;

namespace NotificationForwarder.IntegrationTests.Processing;

/// <summary>What the worker does with accepted notifications: the rate limit, the AI fallback, and surviving a rejected send.</summary>
public sealed class NotificationForwardingTests(ITestOutputHelper output) : IDisposable
{
    private readonly TestWebApplicationFactory _factory = new(output);

    [Fact]
    public async Task WorkerWhenMoreThanTenArriveInAMinuteShouldSendTenThenTheRestAfterTheWindowInOrder()
    {
        var client = _factory.CreateClient();
        const int burst = DeliveryRateLimit.MaximumMessages + 5;
        List<Guid> accepted = [];
        for (var i = 0; i < burst; i++)
        {
            var response = await client.PostNotificationAsync("error", $"failure #{i}", null, TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.Accepted, "intake must never wait on the outbound limit");
            accepted.Add((await response.Content.ReadFromJsonAsync<ReceiveNotificationResponse>(TestContext.Current.CancellationToken))!.Id);
        }

        // Generation happens before the gate, so "eleven generated, ten sent" means the eleventh is parked at the gate.
        (await TestWebApplicationFactory.WaitUntilAsync(() => _factory.GeneratorCalls == DeliveryRateLimit.MaximumMessages + 1, TestContext.Current.CancellationToken))
            .ShouldBeTrue("the worker never reached the eleventh notification");
        _factory.SentNotifications.Count.ShouldBe(DeliveryRateLimit.MaximumMessages, "an eleventh alert left inside the window");

        _factory.Clock.Advance(DeliveryRateLimit.Window - TimeSpan.FromSeconds(1));
        _factory.SentNotifications.Count.ShouldBe(DeliveryRateLimit.MaximumMessages, "an alert left one second before the window slid");

        _factory.Clock.Advance(TimeSpan.FromSeconds(2));

        (await _factory.WaitForSendsAsync(burst, TestContext.Current.CancellationToken)).ShouldBeTrue("the backlog did not drain after the window slid");
        _factory.SentNotifications.Select(notification => notification.Id).ShouldBe(accepted);
    }

    [Fact]
    public async Task WorkerWhenTheAiGeneratorFailsShouldSendTheTemplateAlertInstead()
    {
        _factory.AlertGenerator.GenerateAsync(Arg.Any<NotificationEntity>(), Arg.Any<CancellationToken>())
            .Returns<GeneratedAlert>(_ => throw new AlertGenerationException("The AI provider answered HTTP 503."));
        var client = _factory.CreateClient();

        var response = await client.PostNotificationAsync("error", "SqlException: deadlock victim", "orders-api", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await _factory.WaitForSendsAsync(1, TestContext.Current.CancellationToken)).ShouldBeTrue("the alert never reached the sender");
        var alert = (GeneratedAlert)_factory.AlertSender.ReceivedCalls().Single().GetArguments()[1]!;
        alert.Kind.ShouldBe("Database problem");
        alert.Title.ShouldBe("Database problem reported by orders-api");
        alert.Message.ShouldStartWith("orders-api reported: SqlException: deadlock victim");
    }

    [Fact]
    public async Task WorkerWhenDiscordRejectsOneAlertShouldStillDeliverTheNext()
    {
        var client = _factory.CreateClient();
        _factory.AlertSender.SendAsync(Arg.Is<NotificationEntity>(n => n.Message.Value == "rejected"), Arg.Any<GeneratedAlert>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new AlertDeliveryException("Discord rejected the message with HTTP 400.", 400)));

        await client.PostNotificationAsync("error", "rejected", null, TestContext.Current.CancellationToken);
        await client.PostNotificationAsync("error", "delivered", null, TestContext.Current.CancellationToken);

        (await _factory.WaitForSendsAsync(2, TestContext.Current.CancellationToken)).ShouldBeTrue("the worker stopped after the rejected alert");
        _factory.SentNotifications.Select(notification => notification.Message.Value).ShouldBe(["rejected", "delivered"]);
    }

    public void Dispose() => _factory.Dispose();
}
