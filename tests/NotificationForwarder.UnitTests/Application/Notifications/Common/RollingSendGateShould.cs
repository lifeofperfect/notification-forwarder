namespace NotificationForwarder.UnitTests.Application.Notifications.Common;

public sealed class RollingSendGateShould
{
    private readonly FakeTimeProvider _clock = new(NotificationBuilder.ReceivedAt);

    [Fact]
    public void AllowTenTurnsThenRefuseUntilTheWindowSlides()
    {
        var gate = new RollingSendGate(_clock);

        for (var i = 0; i < DeliveryRateLimit.MaximumMessages; i++)
        {
            gate.TryTakeTurn().ShouldBe(TimeSpan.Zero, $"turn {i + 1}");
        }

        gate.TryTakeTurn().ShouldBe(DeliveryRateLimit.Window);

        _clock.Advance(DeliveryRateLimit.Window);
        gate.TryTakeTurn().ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void ReleaseTurnsOneAtATimeAsSendsAge()
    {
        var gate = new RollingSendGate(_clock);
        gate.TryTakeTurn().ShouldBe(TimeSpan.Zero);                     // t = 0
        _clock.Advance(TimeSpan.FromSeconds(20));
        for (var i = 0; i < DeliveryRateLimit.MaximumMessages - 1; i++)
        {
            gate.TryTakeTurn().ShouldBe(TimeSpan.Zero);                 // t = 20
        }

        gate.TryTakeTurn().ShouldBe(TimeSpan.FromSeconds(40));          // first turn frees at t = 60

        _clock.Advance(TimeSpan.FromSeconds(40));
        gate.TryTakeTurn().ShouldBe(TimeSpan.Zero);                     // t = 60: exactly one free
        gate.TryTakeTurn().ShouldBe(TimeSpan.FromSeconds(20));          // next free at t = 80
    }

    [Fact]
    public void IgnoreWallClockAdjustments()
    {
        var clock = new SplitClock();
        var gate = new RollingSendGate(clock);
        for (var i = 0; i < DeliveryRateLimit.MaximumMessages; i++)
        {
            gate.TryTakeTurn().ShouldBe(TimeSpan.Zero);
        }

        clock.WallClock += DeliveryRateLimit.Window + TimeSpan.FromSeconds(1); // NTP or a manual change
        gate.TryTakeTurn().ShouldBe(DeliveryRateLimit.Window, "a wall-clock jump unlocked capacity");

        clock.Elapsed += DeliveryRateLimit.Window; // real time passed
        gate.TryTakeTurn().ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public async Task HonourCancellationWhileWaiting()
    {
        var gate = new RollingSendGate(_clock);
        for (var i = 0; i < DeliveryRateLimit.MaximumMessages; i++)
        {
            await gate.WaitForTurnAsync(TestContext.Current.CancellationToken);
        }

        using var cancellation = new CancellationTokenSource();
        var pending = gate.WaitForTurnAsync(cancellation.Token);
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => pending);
    }

    /// <summary>A clock whose wall time and monotonic time move independently, to show which one the gate reads.</summary>
    private sealed class SplitClock : TimeProvider
    {
        public DateTimeOffset WallClock { get; set; } = NotificationBuilder.ReceivedAt;

        public TimeSpan Elapsed { get; set; }

        public override DateTimeOffset GetUtcNow() => WallClock;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => Elapsed.Ticks;
    }
}
