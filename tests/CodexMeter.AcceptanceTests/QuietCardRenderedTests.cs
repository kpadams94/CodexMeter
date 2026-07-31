using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CodexMeter;

namespace CodexMeter.AcceptanceTests;

public sealed class QuietCardRenderedTests
{
    [Fact]
    public void Application_window_is_a_frameless_quiet_card_widget()
    {
        RunOnStaThread(() =>
        {
            var window = new MainWindow();

            Assert.Equal(WindowStyle.None, window.WindowStyle);
            Assert.Equal(ResizeMode.NoResize, window.ResizeMode);
            Assert.True(window.AllowsTransparency);
            Assert.False(window.ShowInTaskbar);
            Assert.True(window.Topmost);
            Assert.Equal(143, window.Width);
            Assert.Equal(49, window.Height);
            Assert.IsType<QuietCard>(window.Content);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(47)]
    [InlineData(99)]
    [InlineData(100)]
    public void Representative_usage_values_render_as_a_fixed_noninteractive_card(int percentage)
    {
        RunOnStaThread(() =>
        {
            var card = new QuietCard { RemainingPercentage = percentage };
            card.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            card.Arrange(new Rect(card.DesiredSize));
            card.UpdateLayout();

            var rendering = new RenderTargetBitmap(
                (int)card.ActualWidth,
                (int)card.ActualHeight,
                96,
                96,
                PixelFormats.Pbgra32);
            rendering.Render(card);

            Assert.Equal(143, rendering.PixelWidth);
            Assert.Equal(49, rendering.PixelHeight);
            Assert.Equal(
                $"{percentage} percent of weekly Codex usage remaining",
                AutomationProperties.GetName(card));
            Assert.DoesNotContain(Descendants(card), element => element is RangeBase);
        });
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            yield return child;

            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static void RunOnStaThread(Action assertion)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                assertion();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
