namespace NotificationForwarder.Tests.Common.Http;

/// <summary>Stands in for the network under an adapter: records every request and answers with a canned response.</summary>
public sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    public List<string> Bodies { get; } = [];

    public static FakeHttpMessageHandler Returning(HttpStatusCode status, string? body = null) =>
        new(_ => body is null
            ? new HttpResponseMessage(status)
            : new HttpResponseMessage(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") });

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Body first, then the request: a test that waits on Requests.Count can then read Bodies at the same index.
        Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
        Requests.Add(request);
        return responder(request);
    }
}
