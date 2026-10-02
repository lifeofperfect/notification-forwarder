namespace NotificationForwarder.IntegrationTests.Utilities;

internal static class HttpClientExtensions
{
    public static Task<HttpResponseMessage> PostNotificationAsync(this HttpClient client, string level, string message, string? source, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync(TestWebApplicationFactory.Route, new { level, message, source }, cancellationToken);
}
