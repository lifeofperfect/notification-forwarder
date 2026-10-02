using System.Net;

namespace NotificationForwarder.Infrastructure.Generation.Services.Implementation;

/// <summary>
/// Turns a non-success provider response into an exception that carries only the status code, so provider error
/// bodies never reach logs or results.
/// </summary>
public sealed class OpenAiResponseHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        using (response)
        {
            throw new OpenAiHttpException(response.StatusCode);
        }
    }
}

internal sealed class OpenAiHttpException(HttpStatusCode statusCode)
    : HttpRequestException("The AI provider rejected the request.", null, statusCode);
