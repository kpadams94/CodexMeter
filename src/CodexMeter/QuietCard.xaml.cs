using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace CodexMeter;

public partial class QuietCard : UserControl
{
    public static readonly DependencyProperty RemainingUsageProperty = DependencyProperty.Register(
        nameof(RemainingUsage),
        typeof(RemainingPercentage),
        typeof(QuietCard),
        new FrameworkPropertyMetadata(RemainingPercentage.From(47), OnUsageChanged));

    public QuietCard()
    {
        InitializeComponent();
        UpdatePresentation(RemainingUsage);
    }

    public RemainingPercentage RemainingUsage
    {
        get => (RemainingPercentage)GetValue(RemainingUsageProperty);
        set => SetValue(RemainingUsageProperty, value);
    }

    public void ShowChecking()
    {
        PercentageLabel.Text = "Checking";
        ProgressFill.Width = 0;
        AutomationProperties.SetName(this, "Checking weekly Codex usage");
    }

    public void ShowUsage(RemainingPercentage remainingPercentage)
    {
        SetCurrentValue(RemainingUsageProperty, remainingPercentage);
        UpdatePresentation(remainingPercentage);
    }

    private static void OnUsageChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        var card = (QuietCard)dependencyObject;
        card.UpdatePresentation((RemainingPercentage)eventArgs.NewValue);
    }

    private void UpdatePresentation(RemainingPercentage remainingPercentage)
    {
        var percentage = remainingPercentage.Value;
        PercentageLabel.Text = $"{percentage}% left";
        ProgressFill.Width = QuietCardMetrics.ProgressTrackWidth * percentage / 100d;
        AutomationProperties.SetName(
            this,
            $"{percentage} percent of weekly Codex usage remaining");
    }
}
