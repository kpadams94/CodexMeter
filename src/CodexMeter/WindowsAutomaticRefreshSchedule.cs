using Microsoft.Win32;

namespace CodexMeter;

public sealed class WindowsAutomaticRefreshSchedule : IAutomaticRefreshSchedule, IDisposable
{
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);
    private readonly Timer timer;
    private bool disposed;

    public WindowsAutomaticRefreshSchedule()
    {
        timer = new Timer(OnTimerElapsed);
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    public event Func<Task>? RefreshRequested;

    public void Start() => Reset();

    public void Reset()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        timer.Change(Hour, Timeout.InfiniteTimeSpan);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        timer.Dispose();
    }

    private void OnTimerElapsed(object? state) => _ = RequestRefreshAsync();

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            _ = RequestRefreshAsync();
        }
    }

    private async Task RequestRefreshAsync()
    {
        var handlers = RefreshRequested;
        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList().Cast<Func<Task>>())
        {
            await handler();
        }
    }
}
