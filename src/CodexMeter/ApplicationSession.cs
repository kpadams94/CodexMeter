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

public interface IUsageUpdateSource
{
    event Func<Task>? UsageUpdated;
}

public interface IAutomaticRefreshSchedule
{
    event Func<Task>? RefreshRequested;

    void Start();

    void Reset();
}

public interface IUiDispatcher
{
    Task InvokeAsync(Action action);
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
    IWidgetShell Widget,
    IAutomaticRefreshSchedule? AutomaticRefreshSchedule = null,
    IUiDispatcher? UiDispatcher = null);

public sealed class ApplicationSession : IDisposable
{
    private readonly ApplicationSessionAdapters adapters;
    private readonly IUsageUpdateSource? usageUpdates;
    private readonly IUiDispatcher uiDispatcher;

    public ApplicationSession(ApplicationSessionAdapters adapters)
    {
        this.adapters = adapters;
        uiDispatcher = adapters.UiDispatcher ?? new ImmediateUiDispatcher();
        usageUpdates = adapters.UsageSource as IUsageUpdateSource;
        if (usageUpdates is not null)
        {
            usageUpdates.UsageUpdated += RefreshFromPassiveUpdateAsync;
        }

        if (adapters.AutomaticRefreshSchedule is not null)
        {
            adapters.AutomaticRefreshSchedule.RefreshRequested += RefreshAutomaticallyAsync;
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await RestoreAsync(cancellationToken);
        await RefreshAsync(cancellationToken);
        StartAutomaticRefreshes();
    }

    public void StartAutomaticRefreshes() => adapters.AutomaticRefreshSchedule?.Start();

    public async Task RestoreAsync(CancellationToken cancellationToken = default)
    {
        var savedState = await adapters.UsageStateStore.LoadAsync(cancellationToken);
        if (savedState is null)
        {
            await uiDispatcher.InvokeAsync(adapters.Widget.ShowChecking);
        }
        else
        {
            await uiDispatcher.InvokeAsync(() => adapters.Widget.ShowUsage(savedState));
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
        await uiDispatcher.InvokeAsync(() => adapters.Widget.ShowUsage(state));
    }

    public void Dispose()
    {
        if (usageUpdates is not null)
        {
            usageUpdates.UsageUpdated -= RefreshFromPassiveUpdateAsync;
        }

        if (adapters.AutomaticRefreshSchedule is not null)
        {
            adapters.AutomaticRefreshSchedule.RefreshRequested -= RefreshAutomaticallyAsync;
            (adapters.AutomaticRefreshSchedule as IDisposable)?.Dispose();
        }

        (adapters.UsageSource as IDisposable)?.Dispose();
    }

    private async Task RefreshAutomaticallyAsync()
    {
        try
        {
            await RefreshAsync();
        }
        finally
        {
            adapters.AutomaticRefreshSchedule?.Reset();
        }
    }

    private Task RefreshFromPassiveUpdateAsync() => RefreshAsync();

    private sealed class ImmediateUiDispatcher : IUiDispatcher
    {
        public Task InvokeAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }
    }
}
