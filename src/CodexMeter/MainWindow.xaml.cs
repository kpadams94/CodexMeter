using System.Windows;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace CodexMeter;

public partial class MainWindow : Window, IWidgetShell, IDesktopWidget
{
    private const int GwlExStyle = -20;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExAppWindow = 0x00040000L;
    private const long WsExNoActivate = 0x08000000L;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
    }

    public event Func<WidgetCommand, Task>? CommandRequested;

    public void ShowChecking()
    {
        Card.ToolTip = null;
        Card.ShowChecking();
    }

    public void ShowUsage(UsageState state)
    {
        Card.ToolTip = state.CheckedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        Card.ShowUsage(state.Remaining);
    }

    private async void OnMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        await RaiseCommandRequestedAsync(WidgetCommand.Refresh);

    private async void OnRefreshNowClick(object sender, RoutedEventArgs e) =>
        await RaiseCommandRequestedAsync(WidgetCommand.Refresh);

    private async void OnAboutClick(object sender, RoutedEventArgs e) =>
        await RaiseCommandRequestedAsync(WidgetCommand.About);

    private async void OnExitClick(object sender, RoutedEventArgs e) =>
        await RaiseCommandRequestedAsync(WidgetCommand.Exit);

    private Task RaiseCommandRequestedAsync(WidgetCommand command) =>
        WidgetCommandDispatcher.RaiseAsync(CommandRequested, command);

    public void MoveTo(double left, double top)
    {
        Left = left;
        Top = top;
    }

    public void SetTopmost() => Topmost = true;

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var style = GetWindowLongPtr(handle, GwlExStyle).ToInt64();
        style = (style | WsExToolWindow | WsExNoActivate) & ~WsExAppWindow;
        SetWindowLongPtr(handle, GwlExStyle, new IntPtr(style));
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr handle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr handle, int index, IntPtr value);
}
