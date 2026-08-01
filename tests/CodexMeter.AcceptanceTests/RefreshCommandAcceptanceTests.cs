using CodexMeter;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace CodexMeter.AcceptanceTests;

public sealed partial class ApplicationSessionAcceptanceTests
{
    [Fact]
    public void Widget_exposes_and_routes_refresh_about_and_exit_commands()
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
            AboutDetails? shownAbout = null;
            var currentSessionExitCount = 0;
            var router = new WidgetCommandRouter(
                () => session.RefreshAsync(),
                details => shownAbout = details,
                () => currentSessionExitCount++);
            window.CommandRequested += router.RouteAsync;

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
                Assert.Collection(
                    menu.Items.Cast<object>(),
                    item => Assert.Equal("Refresh Now", Assert.IsType<MenuItem>(item).Header),
                    item => Assert.Equal("About", Assert.IsType<MenuItem>(item).Header),
                    item => Assert.Equal("Exit", Assert.IsType<MenuItem>(item).Header));
                var refreshNow = Assert.IsType<MenuItem>(menu.Items[0]);
                refreshNow.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                var about = Assert.IsType<MenuItem>(menu.Items[1]);
                about.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                var exit = Assert.IsType<MenuItem>(menu.Items[2]);
                exit.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

                var card = Assert.IsType<QuietCard>(window.Content);
                Assert.Equal(3, usageSource.ReadCount);
                Assert.Equal("75 percent of weekly Codex usage remaining", AutomationProperties.GetName(card));
                Assert.Equal(AboutDetails.CodexMeter, shownAbout);
                Assert.Equal(1, currentSessionExitCount);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
