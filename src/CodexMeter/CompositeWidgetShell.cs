namespace CodexMeter;

public sealed class CompositeWidgetShell(params IWidgetShell[] widgets) : IWidgetShell
{
    public void ShowChecking()
    {
        foreach (var widget in widgets)
        {
            widget.ShowChecking();
        }
    }

    public void ShowUsage(UsageState state)
    {
        foreach (var widget in widgets)
        {
            widget.ShowUsage(state);
        }
    }
}
