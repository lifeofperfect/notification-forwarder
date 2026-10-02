using System.Text.RegularExpressions;

namespace NotificationForwarder.Domain.Notifications;

/// <summary>
/// The alert produced for a forwarded notification: what kind of problem it is, a one-line headline and the message
/// for the channel. The generator decides the content; this type only guards the shape. Severity is never part of
/// the headline: the channel message is built from the notification's own level.
/// </summary>
public sealed partial record GeneratedAlert
{
    // Looser than the prompt asks for (an 80-character title) so a slightly long answer is kept rather than thrown away.
    public const int MaximumKindLength = 60;
    public const int MaximumTitleLength = 100;
    public const int MaximumMessageLength = 1_500;

    private GeneratedAlert(string kind, string title, string message) => (Kind, Title, Message) = (kind, title, message);

    /// <summary>A short label naming the kind of warning or error, e.g. "Database connection pool exhausted".</summary>
    public string Kind { get; }

    /// <summary>One line without a severity word; the sender prefixes the notification's level.</summary>
    public string Title { get; }

    /// <summary>A few sentences: what happened and, marked as inference, what it likely means.</summary>
    public string Message { get; }

    public static GeneratedAlert Create(string kind, string title, string message) =>
        TryCreate(kind, title, message) ?? throw new ArgumentException("The alert does not satisfy the shape contract.");

    /// <summary>Builds an alert from untrusted generator output. Returns null when the output does not meet the contract.</summary>
    public static GeneratedAlert? TryCreate(string? kind, string? title, string? message)
    {
        var headline = title is null ? null : SeverityPrefix().Replace(title, string.Empty);
        if (!IsSingleLine(kind, MaximumKindLength) || !IsSingleLine(headline, MaximumTitleLength) || !IsText(message, MaximumMessageLength))
        {
            return null;
        }

        return new GeneratedAlert(kind!.Trim(), headline!.Trim(), message!.Trim());
    }

    private static bool IsSingleLine(string? value, int maximumLength) =>
        IsText(value, maximumLength) && !value!.Any(character => character is '\r' or '\n');

    private static bool IsText(string? value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Trim().Length <= maximumLength
        && !value.Any(character => char.IsControl(character) && character is not ('\r' or '\n' or '\t'));

    // A generator that ignores the instruction and writes "ERROR: ..." must not produce "ERROR: ERROR: ..." downstream.
    [GeneratedRegex(@"^\s*(trace|debug|info|warning|error|critical)\s*[:\-]\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SeverityPrefix();
}
