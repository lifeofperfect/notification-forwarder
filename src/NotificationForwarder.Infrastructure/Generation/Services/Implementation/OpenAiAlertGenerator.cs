using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NotificationForwarder.Infrastructure.Generation.Services.Implementation;

/// <summary>
/// Asks an OpenAI chat model to determine what kind of warning or error the notification is and to write the alert.
/// A strict JSON schema is requested so the reply can be validated before it goes anywhere; anything that does not
/// meet the contract is reported as an <see cref="AlertGenerationException"/>, never delivered.
/// </summary>
public sealed class OpenAiAlertGenerator(IHttpClientFactory clients, IOptions<AiOptions> options, TimeProvider clock) : IAlertGenerator
{
    public const string HttpClientName = "OpenAi";

    private static readonly JsonSerializerOptions WireJson = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions OutputJson = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        MaxDepth = 16
    };

    private static readonly JsonElement OutputSchema = JsonSerializer.SerializeToElement(new
    {
        type = "object",
        additionalProperties = false,
        required = new[] { "kind", "title", "message" },
        properties = new
        {
            kind = new { type = "string", description = "Two to five words naming the kind of problem, without severity words." },
            title = new { type = "string", description = "One line of at most 80 characters naming what happened and where, without the level or any severity word." },
            message = new { type = "string", description = "Two or three sentences written as the alert itself." }
        }
    });

    public async Task<GeneratedAlert> GenerateAsync(NotificationEntity notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var settings = options.Value;
        using var timeout = new CancellationTokenSource(settings.RequestTimeout, clock);
        using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken);
        using var http = clients.CreateClient(HttpClientName);

        var request = new ChatCompletionRequest(
            settings.Model!,
            [
                new ChatMessage("system", NotificationPrompt.Text),
                new ChatMessage("user", JsonSerializer.Serialize(new
                {
                    level = notification.Level.ToString(),
                    source = notification.Source,
                    message = notification.Message.Value
                }))
            ],
            new ResponseFormat("json_schema", new JsonSchemaFormat("notification_alert", true, OutputSchema)),
            Store: false); // not kept for the OpenAI dashboard or training; their abuse-monitoring retention still applies

        using var message = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(request, options: WireJson)
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);

        try
        {
            using var response = await http.SendAsync(message, requestCancellation.Token);
            var completion = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(WireJson, requestCancellation.Token);
            return Parse(completion);
        }
        catch (OpenAiHttpException exception)
        {
            throw new AlertGenerationException($"The AI provider answered HTTP {(int?)exception.StatusCode}.", exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AlertGenerationException($"The AI request exceeded {settings.RequestTimeout.TotalSeconds:0} seconds.", exception);
        }
        catch (HttpRequestException exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new AlertGenerationException($"The AI request failed in transport ({exception.HttpRequestError}).", exception);
        }
        catch (JsonException exception)
        {
            throw new AlertGenerationException("The AI provider returned output that is not the expected JSON.", exception);
        }
    }

    private static GeneratedAlert Parse(ChatCompletionResponse? completion)
    {
        if (completion?.Choices is not [{ } choice])
        {
            throw new AlertGenerationException("The AI provider returned no choice.");
        }

        if (choice.Message?.Refusal is { Length: > 0 })
        {
            throw new AlertGenerationException("The AI provider refused the request.");
        }

        if (choice.FinishReason != "stop")
        {
            throw new AlertGenerationException($"The AI provider stopped with reason {choice.FinishReason ?? "unknown"}.");
        }

        if (choice.Message?.Content is not { Length: > 0 } content)
        {
            throw new AlertGenerationException("The AI provider returned an empty message.");
        }

        var fields = JsonSerializer.Deserialize<GeneratedFields>(content, OutputJson);
        return GeneratedAlert.TryCreate(fields?.Kind, fields?.Title, fields?.Message)
            ?? throw new AlertGenerationException("The AI output did not satisfy the kind, title and message contract.");
    }

    // --- wire types: OpenAI chat completions ---

    private sealed record ChatCompletionRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<ChatMessage> Messages,
        [property: JsonPropertyName("response_format")] ResponseFormat ResponseFormat,
        [property: JsonPropertyName("store")] bool Store);

    private sealed record ChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record ResponseFormat(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("json_schema")] JsonSchemaFormat JsonSchema);

    private sealed record JsonSchemaFormat(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("strict")] bool Strict,
        [property: JsonPropertyName("schema")] JsonElement Schema);

    private sealed record ChatCompletionResponse(
        [property: JsonPropertyName("choices")] IReadOnlyList<Choice>? Choices);

    private sealed record Choice(
        [property: JsonPropertyName("finish_reason")] string? FinishReason,
        [property: JsonPropertyName("message")] ChoiceMessage? Message);

    private sealed record ChoiceMessage(
        [property: JsonPropertyName("content")] string? Content,
        [property: JsonPropertyName("refusal")] string? Refusal);

    private sealed record GeneratedFields(
        [property: JsonPropertyName("kind")] string? Kind,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("message")] string? Message);
}
