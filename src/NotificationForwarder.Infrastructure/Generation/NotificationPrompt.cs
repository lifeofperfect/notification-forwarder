namespace NotificationForwarder.Infrastructure.Generation;

public static class NotificationPrompt
{
    public static string Text { get; } = Load();

    private static string Load()
    {
        using var stream = typeof(NotificationPrompt).Assembly.GetManifestResourceStream("NotificationPrompt.v2")
            ?? throw new InvalidOperationException("The notification prompt resource is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
