namespace NotificationForwarder.Infrastructure.Generation;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>When false the deterministic template generator is used and no credential is required.</summary>
    public bool Enabled { get; init; }

    public string? ApiKey { get; init; }

    /// <summary>An OpenAI chat model that supports structured JSON output, e.g. gpt-5.4.</summary>
    public string? Model { get; init; }

    /// <summary>Must end with a slash. Change it only for a proxy or an OpenAI-compatible endpoint.</summary>
    public Uri BaseUrl { get; init; } = new("https://api.openai.com/v1/");

    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(15);
}
