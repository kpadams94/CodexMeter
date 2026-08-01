using CodexMeter;

namespace CodexMeter.AcceptanceTests;

public sealed class WindowsShellAcceptanceTests
{
    [Fact]
    public void Prevents_a_duplicate_launch()
    {
        var instanceName = $@"Local\CodexMeter.AcceptanceTests.{Guid.NewGuid():N}";
        var firstLaunch = SingleInstanceLease.TryAcquire(instanceName);

        Assert.NotNull(firstLaunch);
        using (firstLaunch)
        {
            using var duplicateLaunch = SingleInstanceLease.TryAcquire(instanceName);
            Assert.Null(duplicateLaunch);
        }
    }

    [Fact]
    public async Task Routes_widget_commands_without_changing_future_startup()
    {
        var refreshCount = 0;
        AboutDetails? shownAbout = null;
        var currentSessionExitCount = 0;
        var router = new WidgetCommandRouter(
            () =>
            {
                refreshCount++;
                return Task.CompletedTask;
            },
            details => shownAbout = details,
            () => currentSessionExitCount++);

        await router.RouteAsync(WidgetCommand.Refresh);
        await router.RouteAsync(WidgetCommand.About);
        await router.RouteAsync(WidgetCommand.Exit);

        Assert.Equal(1, refreshCount);
        Assert.Equal(new AboutDetails("Codex Meter", "Codex Meter"), shownAbout);
        Assert.Equal(1, currentSessionExitCount);
    }

    [Fact]
    public async Task Exit_releases_only_the_current_instance_so_a_future_launch_can_run()
    {
        var instanceName = $@"Local\CodexMeter.AcceptanceTests.{Guid.NewGuid():N}";
        var currentInstance = Assert.IsType<SingleInstanceLease>(
            SingleInstanceLease.TryAcquire(instanceName));
        var router = new WidgetCommandRouter(
            () => Task.CompletedTask,
            _ => { },
            currentInstance.Dispose);

        await router.RouteAsync(WidgetCommand.Exit);

        using var futureLaunch = SingleInstanceLease.TryAcquire(instanceName);
        Assert.NotNull(futureLaunch);
    }

    [Fact]
    public void Tray_exposes_and_routes_refresh_about_and_exit_commands()
    {
        StaThread.Run(() =>
        {
            using var tray = new TrayWidgetShell();
            var routedCommands = new List<WidgetCommand>();
            tray.CommandRequested += command =>
            {
                routedCommands.Add(command);
                return Task.CompletedTask;
            };

            Assert.Collection(
                tray.CommandMenu.Items.Cast<object>(),
                item => Assert.Equal("Refresh Now", Assert.IsType<System.Windows.Forms.ToolStripMenuItem>(item).Text),
                item => Assert.Equal("About", Assert.IsType<System.Windows.Forms.ToolStripMenuItem>(item).Text),
                item => Assert.Equal("Exit", Assert.IsType<System.Windows.Forms.ToolStripMenuItem>(item).Text));

            foreach (System.Windows.Forms.ToolStripMenuItem item in tray.CommandMenu.Items)
            {
                item.PerformClick();
            }

            Assert.Equal(
                [WidgetCommand.Refresh, WidgetCommand.About, WidgetCommand.Exit],
                routedCommands);
        });
    }
}
