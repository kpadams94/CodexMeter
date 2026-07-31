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
            new SampleUsageSource(RemainingPercentage.From(47)),
            new SystemClock(),
            new NoOpUsageStateStore(),
            new CurrentDesktopState(),
            new NoOpNotificationSink(),
            window);

        await new ApplicationSession(adapters).StartAsync();
    }
}
