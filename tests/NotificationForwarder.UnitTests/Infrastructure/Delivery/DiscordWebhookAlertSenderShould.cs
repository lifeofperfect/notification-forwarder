using NotificationForwarder.Infrastructure.Delivery;
using NotificationForwarder.Infrastructure.Delivery.Services.Implementation;

namespace NotificationForwarder.UnitTests.Infrastructure.Delivery;

public sealed class DiscordWebhookAlertSenderShould
{
    private static readonly Uri Webhook = new("https://discord.com/api/webhooks/123/token");

    private static DiscordWebhookAlertSender Create(FakeHttpMessageHandler handler) =>
        new(new HttpClient(handler), Options.Create(new DiscordOptions { Enabled = true, WebhookUrl = Webhook, Username = "Alerts" }),
            new FakeTimeProvider(NotificationBuilder.ReceivedAt), NullLogger<DiscordWebhookAlertSender>.Instance);

    [Fact]
    public async Task PostTheTitleAsContentAndTheRestInOneEmbedWithMentionsSuppressed()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.NoContent);
        var notification = new NotificationBuilder().WithLevel(NotificationLevel.Critical).Build();
        var alert = NotificationBuilder.Alert("Primary database unreachable", "Payments database unreachable", "The primary database is unreachable. This likely means a network partition.");

        await Create(handler).SendAsync(notification, alert, TestContext.Current.CancellationToken);

        var request = handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Post);
        request.RequestUri.ShouldBe(new Uri(Webhook + "?wait=true"));

        using var body = JsonDocument.Parse(handler.Bodies[0]);
        var root = body.RootElement;
        root.GetProperty("username").GetString().ShouldBe("Alerts");
        root.GetProperty("content").GetString().ShouldBe("CRITICAL: " + alert.Title);
        root.GetProperty("allowed_mentions").GetProperty("parse").GetArrayLength().ShouldBe(0);
        var embed = root.GetProperty("embeds").EnumerateArray().ShouldHaveSingleItem();
        embed.GetProperty("title").GetString().ShouldBe(alert.Kind);
        embed.GetProperty("description").GetString().ShouldBe(alert.Message);
        embed.GetProperty("color").GetInt32().ShouldBe(0x8B0000);
        var fields = embed.GetProperty("fields").EnumerateArray()
            .ToDictionary(field => field.GetProperty("name").GetString()!, field => field.GetProperty("value").GetString());
        fields["Level"].ShouldBe("Critical");
        fields["Source"].ShouldBe("payments-api");
        fields["Notification"].ShouldBe(notification.Id.ToString());
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "Discord rejected the message with HTTP 400.")]
    [InlineData(HttpStatusCode.TooManyRequests, "Discord rate-limited the webhook.")]
    public async Task ThrowWithTheStatusWhenDiscordRejectsTheMessage(HttpStatusCode status, string expectedReason)
    {
        var handler = FakeHttpMessageHandler.Returning(status);

        var exception = await Should.ThrowAsync<AlertDeliveryException>(() =>
            Create(handler).SendAsync(new NotificationBuilder().Build(), NotificationBuilder.Alert(), TestContext.Current.CancellationToken));

        exception.StatusCode.ShouldBe((int)status);
        exception.Message.ShouldBe(expectedReason);
    }

    [Fact]
    public async Task ThrowADeliveryExceptionForTransportFailures()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new HttpRequestException("boom", null, HttpStatusCode.BadGateway));

        var exception = await Should.ThrowAsync<AlertDeliveryException>(() =>
            Create(handler).SendAsync(new NotificationBuilder().Build(), NotificationBuilder.Alert(), TestContext.Current.CancellationToken));

        exception.Message.ShouldStartWith("The Discord request failed in transport");
        exception.StatusCode.ShouldBeNull();
    }
}
