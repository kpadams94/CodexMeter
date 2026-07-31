namespace CodexMeter;

public readonly record struct RemainingPercentage
{
    private RemainingPercentage(double value)
    {
        Value = value;
    }

    public double Value { get; }

    public static RemainingPercentage From(double value) => new(Math.Clamp(value, 0, 100));

    public static RemainingPercentage FromUsed(WeeklyUsedPercentage usedPercentage) =>
        From(100 - usedPercentage.Value);
}

public readonly record struct WeeklyUsedPercentage
{
    private WeeklyUsedPercentage(double value)
    {
        Value = value;
    }

    public double Value { get; }

    public static WeeklyUsedPercentage From(double value) => new(value);
}

public sealed record UsageState(RemainingPercentage Remaining, DateTimeOffset CheckedAt);

public interface IUsageSource
{
    Task<WeeklyUsedPercentage?> ReadWeeklyUsedPercentageAsync(CancellationToken cancellationToken);
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public interface IUsageStateStore
{
    Task<UsageState?> LoadAsync(CancellationToken cancellationToken);

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
        await RestoreAsync(cancellationToken);
        await RefreshAsync(cancellationToken);
    }

    public async Task RestoreAsync(CancellationToken cancellationToken = default)
    {
        var savedState = await adapters.UsageStateStore.LoadAsync(cancellationToken);
        if (savedState is null)
        {
            adapters.Widget.ShowChecking();
        }
        else
        {
            adapters.Widget.ShowUsage(savedState);
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        WeeklyUsedPercentage? usedPercentage;
        try
        {
            usedPercentage = await adapters.UsageSource
                .ReadWeeklyUsedPercentageAsync(cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return;
        }
        if (usedPercentage is null)
        {
            return;
        }

        var remainingPercentage = RemainingPercentage.FromUsed(usedPercentage.Value);
        var state = new UsageState(remainingPercentage, adapters.Clock.UtcNow);

        await adapters.UsageStateStore.SaveAsync(state, cancellationToken);
        adapters.Widget.ShowUsage(state);
    }
}
