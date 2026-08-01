using System.Windows;

namespace CodexMeter;

public partial class App : System.Windows.Application
{
    private ApplicationSession? session;
    private TrayWidgetShell? trayWidget;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var window = new MainWindow();
        MainWindow = window;
        trayWidget = new TrayWidgetShell();

        var adapters = new ApplicationSessionAdapters(
            new CodexAppServerUsageSource(),
            new SystemClock(),
            new LocalUsageStateStore(),
            new CurrentDesktopState(),
            new WindowsNotificationSink(),
            new CompositeWidgetShell(window, trayWidget),
            new WindowsAutomaticRefreshSchedule(),
            new WpfUiDispatcher());

        session = new ApplicationSession(adapters);
        window.RefreshRequested += () => session.RefreshAsync();
        trayWidget.RefreshRequested += () => session.RefreshAsync();
        trayWidget.ExitRequested += Shutdown;
        window.ShowChecking();
        window.Show();
        await session.RestoreAsync();
        await session.RefreshAsync();
        session.StartAutomaticRefreshes();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        trayWidget?.Dispose();
        session?.Dispose();
        base.OnExit(e);
    }
}
