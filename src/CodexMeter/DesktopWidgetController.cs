using System.ComponentModel;
using System.Windows;

namespace CodexMeter;

public readonly record struct DesktopWorkArea(double Left, double Top, double Right, double Bottom);

public interface IDesktopWidget
{
    double Width { get; }
    double Height { get; }
    void MoveTo(double left, double top);
    void SetTopmost();
    void Show();
    void Hide();
}

public interface IObservableDesktopState : IDesktopState
{
    event Action? StateChanged;
}

public interface IPrimaryWorkAreaProvider
{
    DesktopWorkArea Current { get; }
    event Action? Changed;
}

public sealed class DesktopWidgetController : IDisposable
{
    private const double WorkAreaInset = 12;
    private readonly IDesktopWidget widget;
    private readonly IObservableDesktopState desktopState;
    private readonly IPrimaryWorkAreaProvider workArea;

    public DesktopWidgetController(IDesktopWidget widget, IObservableDesktopState desktopState, IPrimaryWorkAreaProvider workArea)
    {
        this.widget = widget;
        this.desktopState = desktopState;
        this.workArea = workArea;
    }

    public void Start()
    {
        desktopState.StateChanged += Apply;
        workArea.Changed += Apply;
        Apply();
    }

    public void Dispose()
    {
        desktopState.StateChanged -= Apply;
        workArea.Changed -= Apply;
    }

    private void Apply()
    {
        var area = workArea.Current;
        widget.MoveTo(area.Right - widget.Width - WorkAreaInset, area.Bottom - widget.Height - WorkAreaInset);
        if (desktopState.IsFullScreen)
        {
            widget.Hide();
            return;
        }

        widget.SetTopmost();
        widget.Show();
    }
}

public sealed class SystemPrimaryWorkAreaProvider : IPrimaryWorkAreaProvider, IDisposable
{
    public SystemPrimaryWorkAreaProvider() => SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;

    public DesktopWorkArea Current
    {
        get
        {
            var area = SystemParameters.WorkArea;
            return new DesktopWorkArea(area.Left, area.Top, area.Right, area.Bottom);
        }
    }

    public event Action? Changed;

    public void Dispose() => SystemParameters.StaticPropertyChanged -= OnSystemParametersChanged;

    private void OnSystemParametersChanged(object? sender, PropertyChangedEventArgs e) => Changed?.Invoke();
}
