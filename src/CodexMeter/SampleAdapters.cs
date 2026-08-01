using System.Runtime.InteropServices;

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

public sealed class CurrentDesktopState : IDesktopState, IObservableDesktopState, IDisposable
{
    private const uint EventSystemForeground = 0x0003;
    private const uint EventObjectLocationChange = 0x800B;
    private const uint WineventOutOfContext = 0;
    private const uint MonitorDefaultToNearest = 2;
    private readonly WinEventDelegate callback;
    private readonly System.Windows.Threading.Dispatcher dispatcher;
    private readonly IntPtr foregroundHook;
    private readonly IntPtr locationChangeHook;
    private bool isFullScreen;

    public CurrentDesktopState()
    {
        dispatcher = System.Windows.Application.Current.Dispatcher;
        callback = OnWindowEvent;
        foregroundHook = SetWinEventHook(EventSystemForeground, EventSystemForeground, IntPtr.Zero, callback, 0, 0, WineventOutOfContext);
        locationChangeHook = SetWinEventHook(EventObjectLocationChange, EventObjectLocationChange, IntPtr.Zero, callback, 0, 0, WineventOutOfContext);
        Update();
    }

    public bool IsFullScreen => isFullScreen;
    public event Action? StateChanged;

    public void Dispose()
    {
        if (foregroundHook != IntPtr.Zero) UnhookWinEvent(foregroundHook);
        if (locationChangeHook != IntPtr.Zero) UnhookWinEvent(locationChangeHook);
    }

    private void OnWindowEvent(IntPtr hook, uint eventType, IntPtr window, int objectId, int childId, uint eventThread, uint eventTime)
    {
        if (eventType == EventObjectLocationChange && window != GetForegroundWindow())
        {
            return;
        }

        dispatcher.BeginInvoke(Update);
    }

    private void Update()
    {
        var fullScreen = IsForegroundWindowFullScreen();
        if (fullScreen == isFullScreen) return;
        isFullScreen = fullScreen;
        StateChanged?.Invoke();
    }

    private static bool IsForegroundWindowFullScreen()
    {
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero || !IsWindowVisible(window) || !GetWindowRect(window, out var bounds)) return false;
        var monitor = MonitorFromWindow(window, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero) return false;
        var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        return GetMonitorInfo(monitor, ref monitorInfo)
            && bounds.Left <= monitorInfo.Monitor.Left && bounds.Top <= monitorInfo.Monitor.Top
            && bounds.Right >= monitorInfo.Monitor.Right && bounds.Bottom >= monitorInfo.Monitor.Bottom;
    }

    private delegate void WinEventDelegate(IntPtr hook, uint eventType, IntPtr window, int objectId, int childId, uint eventThread, uint eventTime);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left; public int Top; public int Right; public int Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo { public int Size; public Rect Monitor; public Rect Work; public uint Flags; }

    [DllImport("user32.dll")] private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr module, WinEventDelegate callback, uint processId, uint threadId, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr handle, out Rect rectangle);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr handle, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo monitorInfo);
}

public sealed class NoOpNotificationSink : INotificationSink
{
    public void ShowReset(UsageState state) { }
}
