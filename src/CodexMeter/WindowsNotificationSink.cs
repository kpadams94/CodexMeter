using CommunityToolkit.WinUI.Notifications;

namespace CodexMeter;

public sealed class WindowsNotificationSink : INotificationSink
{
    public void ShowReset(UsageState state)
    {
        new ToastContentBuilder()
            .AddText("Weekly allowance reset")
            .AddText($"{state.Remaining.Value:0}% of your weekly Codex allowance remains.")
            .SetBackgroundActivation()
            .AddAudio(new ToastAudio
            {
                Silent = true,
            })
            .Show();
    }
}
