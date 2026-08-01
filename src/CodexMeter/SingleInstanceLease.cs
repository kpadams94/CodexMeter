namespace CodexMeter;

public sealed class SingleInstanceLease : IDisposable
{
    private readonly Semaphore semaphore;
    private bool disposed;

    private SingleInstanceLease(Semaphore semaphore)
    {
        this.semaphore = semaphore;
    }

    public static SingleInstanceLease? TryAcquire(string instanceName)
    {
        var semaphore = new Semaphore(0, 1, instanceName, out var createdNew);
        if (createdNew || semaphore.WaitOne(0))
        {
            return new SingleInstanceLease(semaphore);
        }

        semaphore.Dispose();
        return null;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        semaphore.Release();
        semaphore.Dispose();
    }
}
