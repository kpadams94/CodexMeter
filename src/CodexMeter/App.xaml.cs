using System.Windows;

namespace CodexMeter;

public partial class App : System.Windows.Application
{
    private const string InstanceName = @"Local\CodexMeter";

    private SingleInstanceLease? singleInstance;
    private ApplicationSession? session;
    private TrayWidgetShell? trayWidget;
    private DesktopWidgetController? desktopWidget;
    private SystemPrimaryWorkAreaProvider? workArea;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        singleInstance = SingleInstanceLease.TryAcquire(InstanceName);
        if (singleInstance is null)
        {
            Shutdown();
            return;
        }

        var window = new MainWindow();
        MainWindow = window;
        trayWidget = new TrayWidgetShell();

        var desktopState = new CurrentDesktopState();
        var adapters = new ApplicationSessionAdapters(
            new CodexAppServerUsageSource(),
            new SystemClock(),
            new LocalUsageStateStore(),
            desktopState,
            new WindowsNotificationSink(),
            new CompositeWidgetShell(window, trayWidget),
            new WindowsAutomaticRefreshSchedule(),
            new WpfUiDispatcher());

        session = new ApplicationSession(adapters);
        var commandRouter = new WidgetCommandRouter(
            () => session.RefreshAsync(),
            ShowAbout,
            Shutdown);
        window.CommandRequested += commandRouter.RouteAsync;
        trayWidget.CommandRequested += commandRouter.RouteAsync;
        window.ShowChecking();
        window.Show();
        workArea = new SystemPrimaryWorkAreaProvider();
        desktopWidget = new DesktopWidgetController(window, desktopState, workArea);
        desktopWidget.Start();
        await session.RestoreAsync();
        await session.RefreshAsync();
        session.StartAutomaticRefreshes();
    }

    private static void ShowAbout(AboutDetails details) =>
        System.Windows.MessageBox.Show(
            details.Description,
            $"About {details.ApplicationName}",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

    protected override void OnExit(ExitEventArgs e)
    {
        trayWidget?.Dispose();
        desktopWidget?.Dispose();
        workArea?.Dispose();
        session?.Dispose();
        singleInstance?.Dispose();
        base.OnExit(e);
    }
}
