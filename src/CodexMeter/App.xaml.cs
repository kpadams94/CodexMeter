using System.Windows;

namespace CodexMeter;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var window = new MainWindow();
        MainWindow = window;

        var adapters = new ApplicationSessionAdapters(
            new CodexAppServerUsageSource(),
            new SystemClock(),
            new LocalUsageStateStore(),
            new CurrentDesktopState(),
            new NoOpNotificationSink(),
            window);

        var session = new ApplicationSession(adapters);
        window.RefreshRequested += () => session.RefreshAsync();
        await session.RestoreAsync();
        window.Show();
        await session.RefreshAsync();
    }
}
