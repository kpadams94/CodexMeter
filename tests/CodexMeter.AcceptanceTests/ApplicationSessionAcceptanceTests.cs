using CodexMeter;
using System.Windows.Automation;

namespace CodexMeter.AcceptanceTests;

public sealed partial class ApplicationSessionAcceptanceTests
{
    [Fact]
    public void Launch_displays_usage_from_the_controlled_source_in_the_production_window()
    {
        RunWindowSession(new ControlledUsageSource(53), (window, session, stateStore) =>
        {
            session.StartAsync().GetAwaiter().GetResult();
            var card = Assert.IsType<QuietCard>(window.Content);

            Assert.True(window.IsVisible);
            Assert.Equal(
                "47 percent of weekly Codex usage remaining",
                AutomationProperties.GetName(card));
            Assert.Equal(47, Assert.Single(stateStore.SavedStates).Remaining.Value);
        });
    }

    [Fact]
    public void Failed_startup_read_stays_silently_in_the_checking_state()
    {
        RunWindowSession(new FailingUsageSource(), (window, session, stateStore) =>
        {
            session.StartAsync().GetAwaiter().GetResult();
            var card = Assert.IsType<QuietCard>(window.Content);

            Assert.Equal("Checking weekly Codex usage", AutomationProperties.GetName(card));
            Assert.Empty(stateStore.SavedStates);
        });
    }

    [Fact]
    public void Missing_weekly_window_stays_in_the_checking_state()
    {
        RunWindowSession(new ControlledUsageSource(null), (window, session, stateStore) =>
        {
            session.StartAsync().GetAwaiter().GetResult();
            var card = Assert.IsType<QuietCard>(window.Content);

            Assert.Equal("Checking weekly Codex usage", AutomationProperties.GetName(card));
            Assert.Empty(stateStore.SavedStates);
        });
    }

    [Fact]
    public void Saved_usage_is_shown_on_restart_when_startup_reconciliation_fails()
    {
        var savedState = new UsageState(
            RemainingPercentage.From(47),
            new DateTimeOffset(2026, 7, 31, 17, 30, 0, TimeSpan.Zero));
        RunWindowSession(new FailingUsageSource(), (window, session, stateStore) =>
        {
            stateStore.InitialState = savedState;

            session.StartAsync().GetAwaiter().GetResult();
            var card = Assert.IsType<QuietCard>(window.Content);

            Assert.Equal("47 percent of weekly Codex usage remaining", AutomationProperties.GetName(card));
            Assert.Equal(savedState.CheckedAt.ToLocalTime().ToString("g"), card.ToolTip);
            Assert.Empty(stateStore.SavedStates);
        });
    }

    [Fact]
    public void Successful_startup_reconciliation_replaces_the_saved_usage_and_check_time()
    {
        var savedState = new UsageState(
            RemainingPercentage.From(47),
            new DateTimeOffset(2026, 7, 31, 17, 30, 0, TimeSpan.Zero));
        RunWindowSession(new ControlledUsageSource(28), (window, session, stateStore) =>
        {
            stateStore.InitialState = savedState;

            session.StartAsync().GetAwaiter().GetResult();
            var card = Assert.IsType<QuietCard>(window.Content);
            var reconciledState = Assert.Single(stateStore.SavedStates);

            Assert.Equal("72 percent of weekly Codex usage remaining", AutomationProperties.GetName(card));
            Assert.Equal(new ControlledClock().UtcNow, reconciledState.CheckedAt);
            Assert.Equal(reconciledState.CheckedAt.ToLocalTime().ToString("g"), card.ToolTip);
        });
    }

    [Fact]
    public void Saved_usage_remains_visible_while_startup_reconciliation_is_pending()
    {
        var savedState = new UsageState(
            RemainingPercentage.From(47),
            new DateTimeOffset(2026, 7, 31, 17, 30, 0, TimeSpan.Zero));
        var usageSource = new DeferredUsageSource();
        RunWindowSession(usageSource, (window, session, stateStore) =>
        {
            stateStore.InitialState = savedState;

            var start = session.StartAsync();
            usageSource.WaitForRead();
            var card = Assert.IsType<QuietCard>(window.Content);

            Assert.Equal("47 percent of weekly Codex usage remaining", AutomationProperties.GetName(card));
            Assert.Equal(savedState.CheckedAt.ToLocalTime().ToString("g"), card.ToolTip);

            usageSource.Complete(28);
            start.GetAwaiter().GetResult();
        });
    }

    [Theory]
    [InlineData(-25, 100)]
    [InlineData(150, 0)]
    public void Out_of_range_usage_is_clamped_when_displayed(double usedPercentage, double expectedRemaining)
    {
        RunWindowSession(
            new ControlledUsageSource(usedPercentage),
            (_, session, stateStore) =>
        {
            session.StartAsync().GetAwaiter().GetResult();

            Assert.Equal(expectedRemaining, Assert.Single(stateStore.SavedStates).Remaining.Value);
        });
    }

    [Fact]
    public void Refresh_updates_after_success_and_preserves_the_display_after_failure()
    {
        var usageSource = new SequencedUsageSource(
            () => Task.FromResult<double?>(53),
            () => Task.FromResult<double?>(28),
            () => throw new InvalidOperationException("Controlled account read failure."));
        RunWindowSession(usageSource, (window, session, stateStore) =>
        {
            session.StartAsync().GetAwaiter().GetResult();
            session.RefreshAsync().GetAwaiter().GetResult();
            session.RefreshAsync().GetAwaiter().GetResult();
            var card = Assert.IsType<QuietCard>(window.Content);

            Assert.Equal("72 percent of weekly Codex usage remaining", AutomationProperties.GetName(card));
            Assert.Equal(2, stateStore.SavedStates.Count);
            Assert.Equal(3, usageSource.ReadCount);
            Assert.Equal(new ControlledClock().UtcNow.ToLocalTime().ToString("g"), card.ToolTip);
        });
    }

    private static void RunWindowSession(
        IUsageSource usageSource,
        Action<MainWindow, ApplicationSession, InMemoryUsageStateStore> assertion)
    {
        StaThread.Run(() =>
        {
            var window = new MainWindow();
            var stateStore = new InMemoryUsageStateStore();
            var adapters = new ApplicationSessionAdapters(
                usageSource,
                new ControlledClock(),
                stateStore,
                new ControlledDesktopState(),
                new RecordingNotificationSink(),
                window);

            window.Show();
            try
            {
                assertion(window, new ApplicationSession(adapters), stateStore);
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

    private sealed class DeferredUsageSource : IUsageSource
    {
        private readonly ManualResetEventSlim readStarted = new();
        private readonly TaskCompletionSource<double?> result = new();

        public async Task<WeeklyUsedPercentage?> ReadWeeklyUsedPercentageAsync(
            CancellationToken cancellationToken)
        {
            readStarted.Set();
            var usedPercentage = await result.Task.WaitAsync(cancellationToken);
            return usedPercentage is null
                ? null
                : WeeklyUsedPercentage.From(usedPercentage.Value);
        }

        public void WaitForRead() => readStarted.Wait(TimeSpan.FromSeconds(2));

        public void Complete(double? usedPercentage) => result.SetResult(usedPercentage);
    }

    private sealed class ControlledClock : IClock
    {
        public DateTimeOffset UtcNow => new(2026, 7, 31, 18, 0, 0, TimeSpan.Zero);
    }

    private sealed class InMemoryUsageStateStore : IUsageStateStore
    {
        public UsageState? InitialState { get; set; }

        public List<UsageState> SavedStates { get; } = [];

        public Task<UsageState?> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(InitialState);

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
