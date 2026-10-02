using System.Threading.Channels;

namespace NotificationForwarder.Infrastructure.Queue.Services.Implementation;

/// <summary>In-memory, bounded, thread-safe queue. Contents do not survive a restart; see docs/decisions.md.</summary>
public sealed class ChannelNotificationBacklog : INotificationBacklog
{
    private readonly Channel<NotificationEntity> _channel;

    public ChannelNotificationBacklog(IOptions<QueueOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _channel = Channel.CreateBounded<NotificationEntity>(new BoundedChannelOptions(options.Value.Capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait // makes TryWrite return false instead of dropping
        });
    }

    public bool TryEnqueue(NotificationEntity notification) => _channel.Writer.TryWrite(notification);

    public IAsyncEnumerable<NotificationEntity> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
