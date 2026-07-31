using CodexMeter;

namespace CodexMeter.AcceptanceTests;

public sealed class ApplicationSessionAcceptanceTests
{
    [Fact]
    public async Task Launch_displays_usage_from_the_controlled_source()
    {
        var widget = new RecordingWidgetShell();
        var adapters = new ApplicationSessionAdapters(
            new ControlledUsageSource(47),
            new ControlledClock(),
            new InMemoryUsageStateStore(),
            new ControlledDesktopState(),
            new RecordingNotificationSink(),
            widget);

        await new ApplicationSession(adapters).StartAsync();

        Assert.Equal(47, widget.RemainingPercentage);
    }

    private sealed class ControlledUsageSource(int remainingPercentage) : IUsageSource
    {
        public Task<int> ReadRemainingPercentageAsync(CancellationToken cancellationToken) =>
            Task.FromResult(remainingPercentage);
    }

    private sealed class ControlledClock : IClock
    {
        public DateTimeOffset UtcNow => new(2026, 7, 31, 18, 0, 0, TimeSpan.Zero);
    }

    private sealed class InMemoryUsageStateStore : IUsageStateStore
    {
        public Task SaveAsync(UsageState state, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class ControlledDesktopState : IDesktopState
    {
        public bool IsFullScreen => false;
    }

    private sealed class RecordingNotificationSink : INotificationSink
    {
        public void ShowReset(UsageState state) { }
    }

    private sealed class RecordingWidgetShell : IWidgetShell
    {
        public int? RemainingPercentage { get; private set; }

        public void ShowChecking() => RemainingPercentage = null;

        public void ShowUsage(UsageState state) => RemainingPercentage = state.RemainingPercentage;
    }
}
