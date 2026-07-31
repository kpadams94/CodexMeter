using System.Windows;

namespace CodexMeter;

public partial class App : Application
{
    private ApplicationSession? session;

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
            window,
            new WindowsAutomaticRefreshSchedule(),
            new WpfUiDispatcher());

        session = new ApplicationSession(adapters);
        window.RefreshRequested += () => session.RefreshAsync();
        await session.RestoreAsync();
        window.Show();
        await session.RefreshAsync();
        session.StartAutomaticRefreshes();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        session?.Dispose();
        base.OnExit(e);
    }
}
