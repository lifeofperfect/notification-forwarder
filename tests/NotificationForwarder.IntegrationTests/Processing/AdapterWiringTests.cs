using System.Text.Json;

namespace NotificationForwarder.IntegrationTests.Processing;

/// <summary>
/// The one test that runs the real OpenAI and Discord adapters inside the host, with only the network faked:
/// proves the typed clients, the enabled-provider wiring and the payloads connect end to end.
/// </summary>
public sealed class AdapterWiringTests(ITestOutputHelper output) : IDisposable
{
    private readonly TestWebApplicationFactory _factory = new(output)
    {
        UseRealAdapters = true,
        OpenAiHttp = FakeHttpMessageHandler.Returning(HttpStatusCode.OK,
            TestWebApplicationFactory.OpenAiCompletion("Database connection pool exhausted", "Payments database pool exhausted", "payments-api ran out of database connections. This likely means a connection leak.")),
        DiscordHttp = FakeHttpMessageHandler.Returning(HttpStatusCode.OK)
    };

    [Fact]
    public async Task PostWithRealAdaptersShouldCallOpenAiThenPostTheGeneratedAlertToDiscord()
    {
        var client = _factory.CreateClient();

        var response = await client.PostNotificationAsync("error", "Npgsql: connection pool exhausted", "payments-api", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var id = (await response.Content.ReadFromJsonAsync<ReceiveNotificationResponse>(TestContext.Current.CancellationToken))!.Id;
        (await TestWebApplicationFactory.WaitUntilAsync(() => _factory.DiscordHttp.Requests.Count == 1, TestContext.Current.CancellationToken))
            .ShouldBeTrue("Discord was never called");

        var openAi = _factory.OpenAiHttp.Requests.ShouldHaveSingleItem();
        openAi.RequestUri!.AbsoluteUri.ShouldBe("https://api.openai.com/v1/chat/completions");
        openAi.Headers.Authorization.ShouldNotBeNull().ToString().ShouldBe("Bearer " + TestWebApplicationFactory.ApiKey);
        _factory.OpenAiHttp.Bodies[0].ShouldContain("Npgsql: connection pool exhausted");

        var discord = _factory.DiscordHttp.Requests.ShouldHaveSingleItem();
        discord.RequestUri!.AbsoluteUri.ShouldBe(TestWebApplicationFactory.WebhookUrl + "?wait=true");
        using var payload = JsonDocument.Parse(_factory.DiscordHttp.Bodies[0]);
        payload.RootElement.GetProperty("content").GetString().ShouldBe("ERROR: Payments database pool exhausted");
        var embed = payload.RootElement.GetProperty("embeds")[0];
        embed.GetProperty("title").GetString().ShouldBe("Database connection pool exhausted");
        embed.GetProperty("description").GetString().ShouldBe("payments-api ran out of database connections. This likely means a connection leak.");
        embed.GetProperty("fields").EnumerateArray().Single(field => field.GetProperty("name").GetString() == "Notification")
            .GetProperty("value").GetString().ShouldBe(id.ToString());
        payload.RootElement.GetProperty("allowed_mentions").GetProperty("parse").GetArrayLength().ShouldBe(0);
    }

    public void Dispose() => _factory.Dispose();
}
