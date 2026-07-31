using CodexMeter;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace CodexMeter.AcceptanceTests;

public sealed partial class ApplicationSessionAcceptanceTests
{
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
}
