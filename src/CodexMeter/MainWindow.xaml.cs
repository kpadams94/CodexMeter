using System.Windows;
using System.Globalization;

namespace CodexMeter;

public partial class MainWindow : Window, IWidgetShell
{
    private const double WorkAreaInset = 12;

    public MainWindow()
    {
        InitializeComponent();
        PlaceAboveTaskbar();
    }

    public event Func<Task>? RefreshRequested;

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
        await RaiseRefreshRequestedAsync();

    private async void OnRefreshNowClick(object sender, RoutedEventArgs e) =>
        await RaiseRefreshRequestedAsync();

    private async Task RaiseRefreshRequestedAsync()
    {
        if (RefreshRequested is null)
        {
            return;
        }

        foreach (Func<Task> handler in RefreshRequested.GetInvocationList())
        {
            await handler();
        }
    }

    private void PlaceAboveTaskbar()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - WorkAreaInset;
        Top = workArea.Bottom - Height - WorkAreaInset;
    }
}
