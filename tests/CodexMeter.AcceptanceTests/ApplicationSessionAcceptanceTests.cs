using CodexMeter;
using System.Windows.Automation;

namespace CodexMeter.AcceptanceTests;

public sealed class ApplicationSessionAcceptanceTests
{
    [Fact]
    public void Launch_displays_usage_from_the_controlled_source_in_the_production_window()
    {
        StaThread.Run(() =>
        {
            var window = new MainWindow();
            var stateStore = new InMemoryUsageStateStore();
            var adapters = new ApplicationSessionAdapters(
                new ControlledUsageSource(RemainingPercentage.From(47)),
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

    private sealed class ControlledUsageSource(RemainingPercentage remainingPercentage) : IUsageSource
    {
        public Task<RemainingPercentage> ReadRemainingPercentageAsync(CancellationToken cancellationToken) =>
            Task.FromResult(remainingPercentage);
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
}
