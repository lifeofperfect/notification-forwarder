namespace NotificationForwarder.Infrastructure.Queue;

public sealed class QueueOptions
{
    public const string SectionName = "Queue";

    /// <summary>Notifications that may wait for processing before intake starts answering 503.</summary>
    public int Capacity { get; init; } = 1_000;
}
