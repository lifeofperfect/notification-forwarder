namespace NotificationForwarder.UnitTests.Common.Builders;

internal sealed class NotificationBuilder
{
    public static readonly DateTimeOffset ReceivedAt = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private NotificationLevel _level = NotificationLevel.Error;
    private string _message = "Database connection pool exhausted after 30 seconds.";
    private string? _source = "payments-api";

    public NotificationBuilder WithLevel(NotificationLevel level)
    {
        _level = level;
        return this;
    }

    public NotificationBuilder WithMessage(string message)
    {
        _message = message;
        return this;
    }

    public NotificationBuilder WithSource(string? source)
    {
        _source = source;
        return this;
    }

    public NotificationEntity Build() => NotificationEntity.Receive(_level, NotificationMessage.Create(_message), _source, ReceivedAt);

    public static GeneratedAlert Alert(
        string kind = "Database connection pool exhausted",
        string title = "Payments database pool exhausted",
        string message = "payments-api reported an exhausted connection pool. This likely means a connection leak or an undersized pool.") =>
        GeneratedAlert.Create(kind, title, message);
}
