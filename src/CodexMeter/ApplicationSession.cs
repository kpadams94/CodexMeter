namespace CodexMeter;

public sealed record UsageState(int RemainingPercentage, DateTimeOffset CheckedAt);

public interface IUsageSource
{
    Task<int> ReadRemainingPercentageAsync(CancellationToken cancellationToken);
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public interface IUsageStateStore
{
    Task SaveAsync(UsageState state, CancellationToken cancellationToken);
}

public interface IDesktopState
{
    bool IsFullScreen { get; }
}

public interface INotificationSink
{
    void ShowReset(UsageState state);
}

public interface IWidgetShell
{
    void ShowChecking();

    void ShowUsage(UsageState state);
}

public sealed record ApplicationSessionAdapters(
    IUsageSource UsageSource,
    IClock Clock,
    IUsageStateStore UsageStateStore,
    IDesktopState DesktopState,
    INotificationSink Notifications,
    IWidgetShell Widget);

public sealed class ApplicationSession(ApplicationSessionAdapters adapters)
{
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        adapters.Widget.ShowChecking();

        var remainingPercentage = await adapters.UsageSource
            .ReadRemainingPercentageAsync(cancellationToken);
        var state = new UsageState(Math.Clamp(remainingPercentage, 0, 100), adapters.Clock.UtcNow);

        await adapters.UsageStateStore.SaveAsync(state, cancellationToken);
        adapters.Widget.ShowUsage(state);
    }
}
