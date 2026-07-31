namespace CodexMeter;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public sealed class NoOpUsageStateStore : IUsageStateStore
{
    public Task<UsageState?> LoadAsync(CancellationToken cancellationToken) =>
        Task.FromResult<UsageState?>(null);

    public Task SaveAsync(UsageState state, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class CurrentDesktopState : IDesktopState
{
    public bool IsFullScreen => false;
}

public sealed class NoOpNotificationSink : INotificationSink
{
    public void ShowReset(UsageState state) { }
}
