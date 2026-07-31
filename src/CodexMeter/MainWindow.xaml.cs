using System.Windows;

namespace CodexMeter;

public partial class MainWindow : Window, IWidgetShell
{
    private const double WorkAreaInset = 12;

    public MainWindow()
    {
        InitializeComponent();
        PlaceAboveTaskbar();
    }

    public void ShowChecking() => Card.ShowChecking();

    public void ShowUsage(UsageState state) => Card.ShowUsage(state.Remaining);

    private void PlaceAboveTaskbar()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - WorkAreaInset;
        Top = workArea.Bottom - Height - WorkAreaInset;
    }
}
