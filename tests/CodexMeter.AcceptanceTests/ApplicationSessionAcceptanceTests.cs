using CodexMeter;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Text.Json;
using System.IO;

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

    [Fact]
    public void Left_click_and_refresh_now_each_read_the_account_again()
    {
        StaThread.Run(() =>
        {
            var window = new MainWindow();
            var usageSource = new SequencedUsageSource(
                () => Task.FromResult<double?>(53),
                () => Task.FromResult<double?>(40),
                () => Task.FromResult<double?>(25));
            var adapters = new ApplicationSessionAdapters(
                usageSource,
                new ControlledClock(),
                new InMemoryUsageStateStore(),
                new ControlledDesktopState(),
                new RecordingNotificationSink(),
                window);
            var session = new ApplicationSession(adapters);
            window.RefreshRequested += () => session.RefreshAsync();

            window.Show();
            try
            {
                session.StartAsync().GetAwaiter().GetResult();
                window.RaiseEvent(new MouseButtonEventArgs(
                    Mouse.PrimaryDevice,
                    Environment.TickCount,
                    MouseButton.Left)
                {
                    RoutedEvent = Mouse.MouseUpEvent,
                });

                var menu = Assert.IsType<ContextMenu>(window.ContextMenu);
                var refreshNow = Assert.IsType<MenuItem>(Assert.Single(menu.Items));
                refreshNow.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

                var card = Assert.IsType<QuietCard>(window.Content);
                Assert.Equal(3, usageSource.ReadCount);
                Assert.Equal("75 percent of weekly Codex usage remaining", AutomationProperties.GetName(card));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Production_session_reads_the_weekly_window_through_codex_app_server()
    {
        StaThread.Run(() =>
        {
            var transcriptPath = Path.Combine(Path.GetTempPath(), $"codex-meter-{Guid.NewGuid():N}.jsonl");
            var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "FakeCodexAppServer.ps1");
            var widget = new RecordingWidgetShell();
            var usageSource = new CodexAppServerUsageSource(
                "powershell.exe",
                "-NoProfile",
                "-NonInteractive",
                "-File",
                fixturePath,
                "-TranscriptPath",
                transcriptPath);
            var adapters = new ApplicationSessionAdapters(
                usageSource,
                new ControlledClock(),
                new InMemoryUsageStateStore(),
                new ControlledDesktopState(),
                new RecordingNotificationSink(),
                widget);

            try
            {
                new ApplicationSession(adapters).StartAsync().GetAwaiter().GetResult();
                var requests = File.ReadAllLines(transcriptPath)
                    .Select(line => JsonDocument.Parse(line))
                    .ToArray();

                Assert.Equal(37, Assert.IsType<UsageState>(widget.LastState).Remaining.Value);
                Assert.Equal("initialize", requests[0].RootElement.GetProperty("method").GetString());
                Assert.Equal("initialized", requests[1].RootElement.GetProperty("method").GetString());
                Assert.Equal("account/rateLimits/read", requests[2].RootElement.GetProperty("method").GetString());
                Assert.DoesNotContain(requests, request =>
                    request.RootElement.GetProperty("method").GetString()!.Contains("login", StringComparison.Ordinal));

                foreach (var request in requests)
                {
                    request.Dispose();
                }
            }
            finally
            {
                File.Delete(transcriptPath);
            }
        });
    }

    private sealed class ControlledUsageSource(double? usedPercentage) : IUsageSource
    {
        public Task<double?> ReadWeeklyUsedPercentageAsync(CancellationToken cancellationToken) =>
            Task.FromResult(usedPercentage);
    }

    private sealed class FailingUsageSource : IUsageSource
    {
        public Task<double?> ReadWeeklyUsedPercentageAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Controlled account read failure.");
    }

    private sealed class SequencedUsageSource(params Func<Task<double?>>[] reads) : IUsageSource
    {
        private readonly Queue<Func<Task<double?>>> remainingReads = new(reads);

        public int ReadCount { get; private set; }

        public Task<double?> ReadWeeklyUsedPercentageAsync(CancellationToken cancellationToken)
        {
            ReadCount++;
            return remainingReads.Dequeue()();
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
