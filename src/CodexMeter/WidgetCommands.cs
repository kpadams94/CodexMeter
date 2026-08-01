namespace CodexMeter;

public enum WidgetCommand
{
    Refresh,
    About,
    Exit,
}

public sealed record AboutDetails(string ApplicationName, string Description)
{
    public static AboutDetails CodexMeter { get; } = new("Codex Meter", "Codex Meter");
}

public sealed class WidgetCommandRouter(
    Func<Task> refresh,
    Action<AboutDetails> showAbout,
    Action exitCurrentSession)
{
    public Task RouteAsync(WidgetCommand command)
    {
        switch (command)
        {
            case WidgetCommand.Refresh:
                return refresh();
            case WidgetCommand.About:
                showAbout(AboutDetails.CodexMeter);
                return Task.CompletedTask;
            case WidgetCommand.Exit:
                exitCurrentSession();
                return Task.CompletedTask;
            default:
                throw new ArgumentOutOfRangeException(nameof(command), command, null);
        }
    }
}

internal static class WidgetCommandDispatcher
{
    public static async Task RaiseAsync(
        Func<WidgetCommand, Task>? handlers,
        WidgetCommand command)
    {
        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList().Cast<Func<WidgetCommand, Task>>())
        {
            await handler(command);
        }
    }
}
