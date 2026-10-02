using Ardalis.Result;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NotificationForwarder.Api.Notifications.ReceiveNotification.V1;

namespace NotificationForwarder.UnitTests.Api.Notifications.ReceiveNotification.V1;

/// <summary>
/// The endpoint with the handler substituted. The integration tests cover the happy paths through the real handler;
/// this pins the request-to-command mapping and the failure paths that need a misbehaving handler or body.
/// </summary>
public sealed class ReceiveNotificationEndpointShould : IDisposable
{
    private const string Route = "/apis/notification-forwarder/v1/notifications";

    private readonly IReceiveNotificationCommandHandler _handler = Substitute.For<IReceiveNotificationCommandHandler>();
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ReceiveNotificationEndpointShould()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IReceiveNotificationCommandHandler>();
                services.AddSingleton(_handler);
            }));
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Return202WithTheIdAndPassEveryFieldToTheHandler()
    {
        var id = Guid.NewGuid();
        ReceiveNotificationCommand? received = null;
        _handler.ReceiveAsync(Arg.Do<ReceiveNotificationCommand>(command => received = command), Arg.Any<CancellationToken>())
            .Returns(Result<ReceiveNotificationResult>.Success(new ReceiveNotificationResult(id, ReceiveNotificationOutcome.Accepted)));

        var response = await _client.PostAsJsonAsync(Route, new { level = "error", message = "DB down", source = "payments" }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var body = await response.Content.ReadFromJsonAsync<ReceiveNotificationResponse>(TestContext.Current.CancellationToken);
        body.ShouldBe(new ReceiveNotificationResponse(id));
        received.ShouldBe(new ReceiveNotificationCommand("error", "DB down", "payments"));
    }

    [Fact]
    public async Task Return500ProblemDetailsWithoutExceptionDetailsWhenTheHandlerThrows()
    {
        _handler.ReceiveAsync(Arg.Any<ReceiveNotificationCommand>(), Arg.Any<CancellationToken>())
            .Returns<Result<ReceiveNotificationResult>>(_ => throw new InvalidOperationException("secret internal detail"));

        var response = await _client.PostAsJsonAsync(Route, new { level = "error", message = "x" }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("\"status\":500");
        body.ShouldNotContain("secret internal detail");
        body.ShouldNotContain("InvalidOperationException");
    }

    [Theory]
    [InlineData("null")]
    [InlineData("")]
    [InlineData("{not json")]
    public async Task Return400ForAnUnreadableBodyWithoutCallingTheHandler(string body)
    {
        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

        var response = await _client.PostAsync(new Uri(Route, UriKind.Relative), content, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await _handler.DidNotReceiveWithAnyArgs().ReceiveAsync(default!, TestContext.Current.CancellationToken);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}
