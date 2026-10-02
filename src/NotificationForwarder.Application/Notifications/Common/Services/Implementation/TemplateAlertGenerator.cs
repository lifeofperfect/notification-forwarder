using System.Text.RegularExpressions;

namespace NotificationForwarder.Application.Notifications.Common.Services.Implementation;

/// <summary>
/// Deterministic generator used when no AI provider is configured, and as the fallback when the provider fails.
/// It guarantees that a warning or error always reaches the channel even during an AI outage, so it must never
/// fail itself: the title is cut to fit and credential-looking values in the quoted text are redacted. The kind it
/// picks is a coarse keyword match; the message says plainly that no assistant was involved.
/// </summary>
public sealed partial class TemplateAlertGenerator : IAlertGenerator
{
    private const int ExcerptLength = 300;

    private static readonly (string Kind, string[] Keywords)[] Rules =
    [
        ("Database problem", ["database", "sql", "deadlock", "query", "connection pool", "npgsql", "sqlexception"]),
        ("Network problem", ["dns", "socket", "timeout", "timed out", "unreachable", "connection refused", "tls", "http 5"]),
        ("Security problem", ["unauthorized", "forbidden", "authentication", "token", "certificate", "permission", "login", "sign-in"]),
        ("Infrastructure problem", ["disk", "memory", "cpu", "pod", "node", "container", "out of space", "kubernetes"]),
        ("Application error", ["exception", "null reference", "stack trace", "unhandled", "invalid operation", "argument"])
    ];

    public Task<GeneratedAlert> GenerateAsync(NotificationEntity notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var kind = Classify(notification.Message.Value);
        var title = notification.Source is null ? kind : $"{kind} reported by {notification.Source}";
        if (title.Length > GeneratedAlert.MaximumTitleLength)
        {
            title = title[..(GeneratedAlert.MaximumTitleLength - 3)] + "...";
        }

        return Task.FromResult(GeneratedAlert.Create(kind, title, BuildMessage(notification)));
    }

    private static string Classify(string message)
    {
        foreach (var (kind, keywords) in Rules)
        {
            if (keywords.Any(keyword => message.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            {
                return kind;
            }
        }

        return "Unclassified notification";
    }

    private static string BuildMessage(NotificationEntity notification)
    {
        var text = Redact(notification.Message.Value);
        if (text.Length > ExcerptLength)
        {
            text = text[..ExcerptLength] + "...";
        }

        var origin = notification.Source ?? "A system";
        return $"{origin} reported: {text} The AI assistant was unavailable, so this alert was built from a template.";
    }

    // The template quotes the notification verbatim, so values that look like credentials are blanked first.
    private static string Redact(string text) =>
        BearerToken().Replace(KeyValueCredential().Replace(text, "$1$2[redacted]"), "$1 [redacted]");

    [GeneratedRegex(@"\b(password|passwd|pwd|secret|token|api[_-]?key|access[_-]?key|authorization)\b(\s*[=:]\s*)(?:bearer\s+)?(""[^""]*""|'[^']*'|\S+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex KeyValueCredential();

    [GeneratedRegex(@"\b(bearer)\s+\S+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BearerToken();
}
