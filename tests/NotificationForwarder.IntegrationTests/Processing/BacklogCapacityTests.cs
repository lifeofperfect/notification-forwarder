namespace NotificationForwarder.IntegrationTests.Processing;

/// <summary>Back-pressure: once the backlog is full, intake answers 503 instead of accepting more than it can hold.</summary>
public sealed class BacklogCapacityTests(ITestOutputHelper output) : IDisposable
{
    private readonly TestWebApplicationFactory _factory = new(output) { QueueCapacity = 1 };

    [Fact]
    public async Task PostWhenTheBacklogIsFullShouldAnswer503WithRetryAfter()
    {
        // Hold the worker inside the first send so the second item sits in the backlog and the third has no room.
        var release = new TaskCompletionSource();
        _factory.AlertSender.SendAsync(Arg.Any<NotificationEntity>(), Arg.Any<GeneratedAlert>(), Arg.Any<CancellationToken>())
            .Returns(_ => release.Task);
        var client = _factory.CreateClient();

        try
        {
            (await client.PostNotificationAsync("error", "first, in flight", null, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Accepted);
            (await _factory.WaitForSendsAsync(1, TestContext.Current.CancellationToken)).ShouldBeTrue("the worker never picked up the first item");
            (await client.PostNotificationAsync("error", "second, waiting", null, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Accepted);

            var rejected = await client.PostNotificationAsync("error", "third, no room", null, TestContext.Current.CancellationToken);

            rejected.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            rejected.Headers.RetryAfter.ShouldNotBeNull().Delta.ShouldBe(TimeSpan.FromSeconds(5));
        }
        finally
        {
            release.SetResult(); // never leave the worker blocked, or host shutdown waits for it
        }
    }

    public void Dispose() => _factory.Dispose();
}
