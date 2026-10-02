using System.Globalization;
using System.Text.Json.Serialization;

namespace NotificationForwarder.Infrastructure.Delivery.Services.Implementation;

/// <summary>Lays the generated alert out as a Discord webhook message: level and title as the one-line content, the rest in one embed.</summary>
internal static class DiscordPayloadMapper
{
    public static DiscordWebhookPayload ToPayload(this NotificationEntity notification, GeneratedAlert alert, string username)
    {
        List<DiscordEmbedField> fields = [new("Level", notification.Level.ToString(), true)];
        if (notification.Source is not null)
        {
            fields.Add(new DiscordEmbedField("Source", notification.Source, true));
        }

        fields.Add(new DiscordEmbedField("Notification", notification.Id.ToString(), false));

        // The severity prefix comes from the notification's own level, never from generated text.
        return new DiscordWebhookPayload(
            username,
            $"{notification.Level.ToString().ToUpperInvariant()}: {alert.Title}",
            [
                new DiscordEmbed(
                    alert.Kind,
                    alert.Message,
                    ColorFor(notification.Level),
                    notification.ReceivedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
                    fields)
            ],
            new DiscordAllowedMentions([])); // never let generated text ping @everyone or roles
    }

    private static int ColorFor(NotificationLevel level) => level switch
    {
        NotificationLevel.Critical => 0x8B0000,
        NotificationLevel.Error => 0xE74C3C,
        NotificationLevel.Warning => 0xF1C40F,
        _ => 0x3498DB
    };
}

internal sealed record DiscordWebhookPayload(
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("embeds")] IReadOnlyList<DiscordEmbed> Embeds,
    [property: JsonPropertyName("allowed_mentions")] DiscordAllowedMentions AllowedMentions);

internal sealed record DiscordEmbed(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("color")] int Color,
    [property: JsonPropertyName("timestamp")] string Timestamp,
    [property: JsonPropertyName("fields")] IReadOnlyList<DiscordEmbedField> Fields);

internal sealed record DiscordEmbedField(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("inline")] bool Inline);

internal sealed record DiscordAllowedMentions(
    [property: JsonPropertyName("parse")] IReadOnlyList<string> Parse);
