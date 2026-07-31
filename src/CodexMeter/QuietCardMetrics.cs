using System.Windows;

namespace CodexMeter;

public static class QuietCardMetrics
{
    public const double Width = 143;
    public const double Height = 49;
    public const double ProgressTrackWidth = 107;

    public static CornerRadius CardCornerRadius { get; } = new(16);
}
