using CodexMeter;
using System.Windows.Automation;

namespace CodexMeter.AcceptanceTests;

public sealed partial class ApplicationSessionAcceptanceTests
{
    [Fact]
    public void Launch_displays_usage_from_the_controlled_source_in_the_production_window()
    {
        StaThread.Run(() =>
        {
            var window = new MainWindow();
            var stateStore = new InMemoryUsageStateStore();
            var adapters = new ApplicationSessionAdapters(
                new ControlledUsageSource(53),
                new ControlledClock(),
                stateStore,
                new ControlledDesktopState(),
                new RecordingNotificationSink(),
                window);

            window.Show();
            try
            {
                new ApplicationSession(adapters).StartAsync().GetAwaiter().GetResult();
                var card = Assert.IsType<QuietCard>(window.Content);

                Assert.True(window.IsVisible);
                Assert.Equal(
                    "47 percent of weekly Codex usage remaining",
                    AutomationProperties.GetName(card));
                Assert.Equal(47, Assert.Single(stateStore.SavedStates).Remaining.Value);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Failed_startup_read_stays_silently_in_the_checking_state()
    {
        StaThread.Run(() =>
        {
            var window = new MainWindow();
            var stateStore = new InMemoryUsageStateStore();
            var adapters = new ApplicationSessionAdapters(
                new FailingUsageSource(),
                new ControlledClock(),
                stateStore,
                new ControlledDesktopState(),
                new RecordingNotificationSink(),
                window);

            window.Show();
            try
            {
                new ApplicationSession(adapters).StartAsync().GetAwaiter().GetResult();
                var card = Assert.IsType<QuietCard>(window.Content);

                Assert.Equal("Checking weekly Codex usage", AutomationProperties.GetName(card));
                Assert.Empty(stateStore.SavedStates);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Missing_weekly_window_stays_in_the_checking_state()
    {
        StaThread.Run(() =>
        {
            var window = new MainWindow();
            var stateStore = new InMemoryUsageStateStore();
            var adapters = new ApplicationSessionAdapters(
                new ControlledUsageSource(null),
                new ControlledClock(),
                stateStore,
                new ControlledDesktopState(),
                new RecordingNotificationSink(),
                window);

            window.Show();
            try
            {
                new ApplicationSession(adapters).StartAsync().GetAwaiter().GetResult();
                var card = Assert.IsType<QuietCard>(window.Content);

                Assert.Equal("Checking weekly Codex usage", AutomationProperties.GetName(card));
                Assert.Empty(stateStore.SavedStates);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(-25, 100)]
    [InlineData(150, 0)]
    public void Out_of_range_usage_is_clamped_when_displayed(double usedPercentage, double expectedRemaining)
    {
        StaThread.Run(() =>
        {
            var window = new MainWindow();
            var stateStore = new InMemoryUsageStateStore();
            var adapters = new ApplicationSessionAdapters(
                new ControlledUsageSource(usedPercentage),
                new ControlledClock(),
                stateStore,
                new ControlledDesktopState(),
                new RecordingNotificationSink(),
                window);

            window.Show();
            try
            {
                new ApplicationSession(adapters).StartAsync().GetAwaiter().GetResult();

                Assert.Equal(expectedRemaining, Assert.Single(stateStore.SavedStates).Remaining.Value);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Refresh_updates_after_success_and_preserves_the_display_after_failure()
    {
        StaThread.Run(() =>
        {
            var window = new MainWindow();
            var stateStore = new InMemoryUsageStateStore();
            var usageSource = new SequencedUsageSource(
                () => Task.FromResult<double?>(53),
                () => Task.FromResult<double?>(28),
                () => throw new InvalidOperationException("Controlled account read failure."));
            var adapters = new ApplicationSessionAdapters(
                usageSource,
                new ControlledClock(),
                stateStore,
                new ControlledDesktopState(),
                new RecordingNotificationSink(),
                window);
            var session = new ApplicationSession(adapters);

            window.Show();
            try
            {
                session.StartAsync().GetAwaiter().GetResult();
                session.RefreshAsync().GetAwaiter().GetResult();
                session.RefreshAsync().GetAwaiter().GetResult();
                var card = Assert.IsType<QuietCard>(window.Content);

                Assert.Equal("72 percent of weekly Codex usage remaining", AutomationProperties.GetName(card));
                Assert.Equal(2, stateStore.SavedStates.Count);
                Assert.Equal(3, usageSource.ReadCount);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private sealed class ControlledUsageSource(double? usedPercentage) : IUsageSource
    {
        public Task<WeeklyUsedPercentage?> ReadWeeklyUsedPercentageAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(usedPercentage is null
                ? (WeeklyUsedPercentage?)null
                : WeeklyUsedPercentage.From(usedPercentage.Value));
    }

    private sealed class FailingUsageSource : IUsageSource
    {
        public Task<WeeklyUsedPercentage?> ReadWeeklyUsedPercentageAsync(
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Controlled account read failure.");
    }

    private sealed class SequencedUsageSource(params Func<Task<double?>>[] reads) : IUsageSource
    {
        private readonly Queue<Func<Task<double?>>> remainingReads = new(reads);

        public int ReadCount { get; private set; }

        public async Task<WeeklyUsedPercentage?> ReadWeeklyUsedPercentageAsync(
            CancellationToken cancellationToken)
        {
            ReadCount++;
            var usedPercentage = await remainingReads.Dequeue()();
            return usedPercentage is null
                ? null
                : WeeklyUsedPercentage.From(usedPercentage.Value);
        }
    }

    private sealed class ControlledClock : IClock
    {
        public DateTimeOffset UtcNow => new(2026, 7, 31, 18, 0, 0, TimeSpan.Zero);
    }

    private sealed class InMemoryUsageStateStore : IUsageStateStore
    {
        public List<UsageState> SavedStates { get; } = [];

        public Task SaveAsync(UsageState state, CancellationToken cancellationToken)
        {
            SavedStates.Add(state);
            return Task.CompletedTask;
        }
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
        public UsageState? LastState { get; private set; }

        public void ShowChecking() { }

        public void ShowUsage(UsageState state) => LastState = state;
    }
}
