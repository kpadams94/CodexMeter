using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace CodexMeter;

public partial class QuietCard : UserControl
{
    private const double ProgressTrackWidth = 107;

    public static readonly DependencyProperty RemainingPercentageProperty = DependencyProperty.Register(
        nameof(RemainingPercentage),
        typeof(int),
        typeof(QuietCard),
        new FrameworkPropertyMetadata(47, OnRemainingPercentageChanged));

    public QuietCard()
    {
        InitializeComponent();
        UpdatePresentation(RemainingPercentage);
    }

    public int RemainingPercentage
    {
        get => (int)GetValue(RemainingPercentageProperty);
        set => SetValue(RemainingPercentageProperty, value);
    }

    public void ShowChecking()
    {
        PercentageLabel.Text = "Checking";
        ProgressFill.Width = 0;
        AutomationProperties.SetName(this, "Checking weekly Codex usage");
    }

    private static void OnRemainingPercentageChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        var card = (QuietCard)dependencyObject;
        var percentage = Math.Clamp((int)eventArgs.NewValue, 0, 100);

        if (percentage != (int)eventArgs.NewValue)
        {
            card.SetCurrentValue(RemainingPercentageProperty, percentage);
            return;
        }

        card.UpdatePresentation(percentage);
    }

    private void UpdatePresentation(int percentage)
    {
        PercentageLabel.Text = $"{percentage}% left";
        ProgressFill.Width = ProgressTrackWidth * percentage / 100d;
        AutomationProperties.SetName(
            this,
            $"{percentage} percent of weekly Codex usage remaining");
    }
}
