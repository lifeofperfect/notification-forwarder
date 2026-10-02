namespace NotificationForwarder.IntegrationTests.Controllers;

/// <summary>The HTTP contract, through the real pipeline, validation, backlog and worker.</summary>
public sealed class ReceiveNotificationEndpointTests(ITestOutputHelper output) : IDisposable
{
    private readonly TestWebApplicationFactory _factory = new(output);

    [Theory]
    [InlineData("warning", "WARNING")]
    [InlineData("error", "ERROR")]
    [InlineData("critical", "CRITICAL")]
    public async Task PostWhenWarningOrHigherShouldQueueAndHandTheNotificationToTheSender(string level, string expectedPrefix)
    {
        var client = _factory.CreateClient();

        var response = await client.PostNotificationAsync(level, "Database connection pool exhausted.", "payments-api", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var body = await response.Content.ReadFromJsonAsync<ReceiveNotificationResponse>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull().Id.ShouldNotBe(Guid.Empty);

        (await _factory.WaitForSendsAsync(1, TestContext.Current.CancellationToken)).ShouldBeTrue("the alert never reached the sender");
        var sent = _factory.SentNotifications.ShouldHaveSingleItem();
        sent.Id.ShouldBe(body.Id);
        sent.Level.ToString().ToUpperInvariant().ShouldBe(expectedPrefix);
        sent.Source.ShouldBe("payments-api");
        sent.Message.Value.ShouldBe("Database connection pool exhausted.");
        await _factory.AlertSender.Received(1).SendAsync(sent, TestWebApplicationFactory.StubAlert, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("trace")]
    [InlineData("debug")]
    [InlineData("info")]
    public async Task PostWhenBelowWarningShouldAnswer204AndNotForward(string level)
    {
        var client = _factory.CreateClient();

        var response = await client.PostNotificationAsync(level, "Nightly run finished", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await Task.Delay(200, TestContext.Current.CancellationToken);
        await _factory.AlertGenerator.DidNotReceiveWithAnyArgs().GenerateAsync(default!, TestContext.Current.CancellationToken);
        await _factory.AlertSender.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PostWhenPayloadIsInvalidShouldReturnValidationProblemWithEveryFieldError()
    {
        var client = _factory.CreateClient();

        var response = await client.PostNotificationAsync("severe", "", new string('s', NotificationEntity.MaximumSourceLength + 1), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken);
        problem.ShouldNotBeNull().Errors.Keys.ShouldBe(["level", "message", "source"], ignoreOrder: true);
    }

    [Fact]
    public async Task GetHealthProbesShouldAnswer200()
    {
        var client = _factory.CreateClient();

        (await client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync(new Uri("/health/ready", UriKind.Relative), TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    public void Dispose() => _factory.Dispose();
}
