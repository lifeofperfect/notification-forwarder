using Microsoft.Extensions.DependencyInjection;
using NotificationForwarder.Application.Notifications.ProcessPendingNotification;
using NotificationForwarder.Infrastructure.Processing;
using NotificationForwarder.Infrastructure.Queue;
using NotificationForwarder.Infrastructure.Queue.Services.Implementation;

namespace NotificationForwarder.UnitTests.Infrastructure.Processing;

public sealed class NotificationProcessingWorkerShould
{
    [Fact]
    public async Task KeepDrainingTheBacklogAfterDeliveryAndUnexpectedFailures()
    {
        var handler = Substitute.For<IProcessPendingNotificationCommandHandler>();
        var rejected = new NotificationBuilder().WithMessage("rejected by Discord").Build();
        var crashed = new NotificationBuilder().WithMessage("unexpected crash").Build();
        var delivered = new NotificationBuilder().WithMessage("delivered").Build();
        handler.ProcessAsync(rejected, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new AlertDeliveryException("Discord rejected the message with HTTP 400.", 400)));
        handler.ProcessAsync(crashed, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("boom")));

        var backlog = new ChannelNotificationBacklog(Options.Create(new QueueOptions { Capacity = 10 }));
        await using var provider = new ServiceCollection().AddSingleton(handler).BuildServiceProvider();
        using var worker = new NotificationProcessingWorker(backlog, provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<NotificationProcessingWorker>.Instance);

        await worker.StartAsync(TestContext.Current.CancellationToken);
        backlog.TryEnqueue(rejected).ShouldBeTrue();
        backlog.TryEnqueue(crashed).ShouldBeTrue();
        backlog.TryEnqueue(delivered).ShouldBeTrue();

        await WaitUntilAsync(() => handler.ReceivedCalls().Count() == 3, TestContext.Current.CancellationToken);
        await worker.StopAsync(TestContext.Current.CancellationToken);

        Received.InOrder(() =>
        {
            handler.ProcessAsync(rejected, Arg.Any<CancellationToken>());
            handler.ProcessAsync(crashed, Arg.Any<CancellationToken>());
            handler.ProcessAsync(delivered, Arg.Any<CancellationToken>());
        });
    }

    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The worker did not process every item in time.");
            }

            await Task.Delay(10, cancellationToken);
        }
    }
}
