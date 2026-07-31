using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
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
        StaThread.Run(() =>
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
        StaThread.Run(() =>
        {
            var card = new QuietCard { Usage = RemainingPercentage.From(percentage) };
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

            var pixels = CopyPixels(rendering);
            var percentageLabel = Descendants(card)
                .OfType<TextBlock>()
                .Single(text => text.Text.EndsWith("% left", StringComparison.Ordinal));
            var brandLabel = Descendants(card)
                .OfType<TextBlock>()
                .Single(text => text.Text == "CODEX");
            var cardBorder = Assert.IsType<Border>(card.Content);

            Assert.Equal(143, rendering.PixelWidth);
            Assert.Equal(49, rendering.PixelHeight);
            Assert.Equal(16, cardBorder.CornerRadius.TopLeft);
            Assert.Equal(10, percentageLabel.FontSize);
            Assert.Equal(TextWrapping.NoWrap, percentageLabel.TextWrapping);
            Assert.Equal(9, brandLabel.FontSize);
            Assert.Equal(TextWrapping.NoWrap, brandLabel.TextWrapping);
            Assert.True(AlphaAt(pixels, rendering.PixelWidth, 71, 24) > 0);
            Assert.Equal(0, AlphaAt(pixels, rendering.PixelWidth, 0, 0));
            Assert.True(HasLightPixels(pixels, rendering.PixelWidth, 12, 8, 75, 26));
            Assert.True(HasLightPixels(pixels, rendering.PixelWidth, 82, 8, 132, 26));

            var purplePixelCount = CountPurplePixels(pixels);
            if (percentage == 0)
            {
                Assert.Equal(0, purplePixelCount);
            }
            else
            {
                Assert.True(purplePixelCount > 0);
            }

            Assert.Equal(
                $"{percentage} percent of weekly Codex usage remaining",
                AutomationProperties.GetName(card));
            Assert.DoesNotContain(Descendants(card), element => element is RangeBase);
        });
    }

    private static byte[] CopyPixels(BitmapSource rendering)
    {
        var stride = rendering.PixelWidth * 4;
        var pixels = new byte[stride * rendering.PixelHeight];
        rendering.CopyPixels(pixels, stride, 0);
        return pixels;
    }

    private static byte AlphaAt(byte[] pixels, int width, int x, int y) =>
        pixels[((y * width) + x) * 4 + 3];

    private static bool HasLightPixels(
        byte[] pixels,
        int width,
        int left,
        int top,
        int right,
        int bottom)
    {
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                var offset = ((y * width) + x) * 4;
                if (pixels[offset] > 70 && pixels[offset + 1] > 70 && pixels[offset + 2] > 70)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int CountPurplePixels(byte[] pixels)
    {
        var count = 0;
        for (var offset = 0; offset < pixels.Length; offset += 4)
        {
            var blue = pixels[offset];
            var green = pixels[offset + 1];
            var red = pixels[offset + 2];
            if (blue > 225 && green is > 80 and < 150 && red is > 145 and < 210)
            {
                count++;
            }
        }

        return count;
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

}
