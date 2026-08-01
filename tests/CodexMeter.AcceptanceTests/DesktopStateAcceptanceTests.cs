using CodexMeter;

namespace CodexMeter.AcceptanceTests;

public sealed class DesktopStateAcceptanceTests
{
    [Fact]
    public void A_normal_maximized_foreground_window_is_not_full_screen()
    {
        var window = new ForegroundWindowSnapshot(
            new WindowBounds(1920, 0, 3840, 1080),
            new WindowBounds(1920, 0, 3840, 1080),
            HasStandardFrame: true);

        Assert.False(FullScreenWindowDetector.IsFullScreen(window));
    }

    [Fact]
    public void A_borderless_foreground_window_covering_its_monitor_is_full_screen()
    {
        var window = new ForegroundWindowSnapshot(
            new WindowBounds(1920, 0, 3840, 1080),
            new WindowBounds(1920, 0, 3840, 1080),
            HasStandardFrame: false);

        Assert.True(FullScreenWindowDetector.IsFullScreen(window));
    }

    [Fact]
    public void A_foreground_window_that_does_not_cover_its_monitor_is_not_full_screen()
    {
        var window = new ForegroundWindowSnapshot(
            new WindowBounds(1920, 0, 3840, 1040),
            new WindowBounds(1920, 0, 3840, 1080),
            HasStandardFrame: false);

        Assert.False(FullScreenWindowDetector.IsFullScreen(window));
    }
}
