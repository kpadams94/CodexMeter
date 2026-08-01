using CodexMeter;

namespace CodexMeter.AcceptanceTests;

public sealed class DesktopWidgetAcceptanceTests
{
    [Fact]
    public void Anchors_above_the_primary_work_area_and_repositions_when_it_changes()
    {
        var widget = new ControlledDesktopWidget(143, 49);
        var desktopState = new ControlledDesktopState();
        var workArea = new ControlledPrimaryWorkArea(new DesktopWorkArea(0, 0, 1920, 1040));
        using var controller = new DesktopWidgetController(widget, desktopState, workArea);

        controller.Start();

        Assert.Equal((1765, 979), widget.Position);
        Assert.True(widget.IsVisible);
        Assert.True(widget.IsTopmost);

        workArea.ChangeTo(new DesktopWorkArea(0, 0, 1600, 900));

        Assert.Equal((1445, 839), widget.Position);
    }

    [Fact]
    public void Hides_for_a_full_screen_foreground_application_and_restores_to_the_anchor_afterward()
    {
        var widget = new ControlledDesktopWidget(143, 49);
        var desktopState = new ControlledDesktopState();
        var workArea = new ControlledPrimaryWorkArea(new DesktopWorkArea(0, 0, 1920, 1040));
        using var controller = new DesktopWidgetController(widget, desktopState, workArea);
        controller.Start();

        desktopState.SetFullScreen(true);

        Assert.False(widget.IsVisible);

        workArea.ChangeTo(new DesktopWorkArea(0, 0, 1600, 900));
        desktopState.SetFullScreen(false);

        Assert.True(widget.IsVisible);
        Assert.Equal((1445, 839), widget.Position);
        Assert.True(widget.IsTopmost);
    }

    private sealed class ControlledDesktopWidget(double width, double height) : IDesktopWidget
    {
        public double Width { get; } = width;

        public double Height { get; } = height;

        public (double Left, double Top) Position { get; private set; }

        public bool IsVisible { get; private set; }

        public bool IsTopmost { get; private set; }

        public void MoveTo(double left, double top) => Position = (left, top);

        public void SetTopmost() => IsTopmost = true;

        public void Show() => IsVisible = true;

        public void Hide() => IsVisible = false;
    }

    private sealed class ControlledDesktopState : IDesktopState, IObservableDesktopState
    {
        public bool IsFullScreen { get; private set; }

        public event Action? StateChanged;

        public void SetFullScreen(bool isFullScreen)
        {
            IsFullScreen = isFullScreen;
            StateChanged?.Invoke();
        }
    }

    private sealed class ControlledPrimaryWorkArea(DesktopWorkArea current) : IPrimaryWorkAreaProvider
    {
        public event Action? Changed;

        public DesktopWorkArea Current => current;

        public void ChangeTo(DesktopWorkArea workArea)
        {
            current = workArea;
            Changed?.Invoke();
        }
    }
}
