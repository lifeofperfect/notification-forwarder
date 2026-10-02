namespace NotificationForwarder.Infrastructure.Delivery;

public sealed class DiscordOptions
{
    public const string SectionName = "Discord";

    /// <summary>When false, alerts are written to the log instead of Discord. Useful for local runs without a webhook.</summary>
    public bool Enabled { get; init; }

    /// <summary>Discord webhook URL (https://discord.com/api/webhooks/{id}/{token}). Treat it as a secret.</summary>
    public Uri? WebhookUrl { get; init; }

    public string Username { get; init; } = "Notification Forwarder";

    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(10);
}
