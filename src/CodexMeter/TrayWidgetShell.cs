using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace CodexMeter;

public sealed class TrayWidgetShell : IWidgetShell, IDisposable
{
    private readonly Icon icon;
    private readonly Forms.NotifyIcon notifyIcon;
    private bool disposed;

    public TrayWidgetShell()
    {
        icon = LoadIcon();
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Refresh Now", null, (_, _) => RequestCommand(WidgetCommand.Refresh));
        menu.Items.Add("About", null, (_, _) => RequestCommand(WidgetCommand.About));
        menu.Items.Add("Exit", null, (_, _) => RequestCommand(WidgetCommand.Exit));

        notifyIcon = new Forms.NotifyIcon
        {
            Icon = icon,
            Text = "Checking weekly Codex usage",
            ContextMenuStrip = menu,
            Visible = true,
        };
    }

    public event Func<WidgetCommand, Task>? CommandRequested;

    internal Forms.ContextMenuStrip CommandMenu => notifyIcon.ContextMenuStrip!;

    public void ShowChecking() => notifyIcon.Text = "Checking weekly Codex usage";

    public void ShowUsage(UsageState state) =>
        notifyIcon.Text = $"Codex Meter: {state.Remaining.Value:0}% weekly remaining";

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
        icon.Dispose();
    }

    private async void RequestCommand(WidgetCommand command) =>
        await WidgetCommandDispatcher.RaiseAsync(CommandRequested, command);

    private static Icon LoadIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "CodexMeterIcon.png");
        using var bitmap = new Bitmap(iconPath);
        var iconHandle = bitmap.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(iconHandle).Clone();
        }
        finally
        {
            DestroyIcon(iconHandle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr iconHandle);
}
