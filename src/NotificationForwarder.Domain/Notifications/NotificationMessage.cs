namespace NotificationForwarder.Domain.Notifications;

/// <summary>The free-text body of a notification, validated and trimmed.</summary>
public sealed record NotificationMessage
{
    public const int MaximumLength = 2_000;

    private NotificationMessage(string value) => Value = value;

    public string Value { get; }

    /// <summary>Returns a validation message, or null when the value is acceptable.</summary>
    public static string? Validate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "A message is required.";
        }

        if (value.Any(character => char.IsControl(character) && character is not ('\r' or '\n' or '\t')))
        {
            return "The message must not contain control characters other than tabs and line breaks.";
        }

        return value.Length > MaximumLength
            ? $"The message must not exceed {MaximumLength} characters."
            : null;
    }

    public static NotificationMessage Create(string value)
    {
        var error = Validate(value);
        if (error is not null)
        {
            throw new ArgumentException(error, nameof(value));
        }

        return new NotificationMessage(value.Trim());
    }
}
