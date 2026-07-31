using System.Windows;

namespace CodexMeter;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var window = new MainWindow();
        MainWindow = window;
        window.Show();

        var adapters = new ApplicationSessionAdapters(
            new CodexAppServerUsageSource(),
            new SystemClock(),
            new NoOpUsageStateStore(),
            new CurrentDesktopState(),
            new NoOpNotificationSink(),
            window);

        var session = new ApplicationSession(adapters);
        window.RefreshRequested += () => session.RefreshAsync();
        await session.StartAsync();
    }
}
