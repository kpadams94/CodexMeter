using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Collections.Concurrent;

namespace CodexMeter;

public sealed class CodexAppServerUsageSource : IUsageSource, IUsageUpdateSource, IDisposable
{
    private const int WeeklyWindowDurationMinutes = 7 * 24 * 60;
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(15);
    private readonly string executablePath;
    private readonly IReadOnlyList<string> arguments;
    private readonly SemaphoreSlim startupLock = new(1, 1);
    private readonly SemaphoreSlim writeLock = new(1, 1);
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonDocument>> pendingResponses = new();
    private Process? process;
    private int nextRequestId = 1;
    private bool disposed;

    public CodexAppServerUsageSource()
        : this(CodexExecutableLocator.Resolve(), "app-server", "--listen", "stdio://")
    {
    }

    public CodexAppServerUsageSource(string executablePath, params string[] arguments)
    {
        this.executablePath = executablePath;
        this.arguments = arguments;
    }

    public event Func<Task>? UsageUpdated;

    public async Task<WeeklyUsedPercentage?> ReadWeeklyUsedPercentageAsync(
        CancellationToken cancellationToken)
    {
        await EnsureStartedAsync(cancellationToken);

        var requestId = Interlocked.Increment(ref nextRequestId);
        using var response = await SendRequestAsync(
            new { method = "account/rateLimits/read", id = requestId },
            requestId,
            cancellationToken);
        var usedPercentage = ReadWeeklyUsedPercentage(response.RootElement.GetProperty("result"));
        return usedPercentage is null
            ? null
            : WeeklyUsedPercentage.From(usedPercentage.Value);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (process is not null)
        {
            StopAppServer(process);
        }

        startupLock.Dispose();
        writeLock.Dispose();
    }

    private async Task EnsureStartedAsync(CancellationToken cancellationToken)
    {
        if (process is not null)
        {
            return;
        }

        await startupLock.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (process is not null)
            {
                return;
            }

            process = StartAppServer();
            _ = process.StandardError.ReadToEndAsync(cancellationToken);
            _ = ReadMessagesAsync(process);

            using (await SendRequestAsync(
                new
                {
                    method = "initialize",
                    id = 1,
                    @params = new
                    {
                        clientInfo = new
                        {
                            name = "codex_meter",
                            title = "Codex Meter",
                            version = "0.1.0",
                        },
                    },
                },
                1,
                cancellationToken))
            {
            }
            await WriteAsync(new { method = "initialized", @params = new { } }, cancellationToken);
        }
        catch
        {
            if (process is not null)
            {
                StopAppServer(process);
                process = null;
            }

            throw;
        }
        finally
        {
            startupLock.Release();
        }
    }

    private async Task<JsonDocument> SendRequestAsync(
        object request,
        int requestId,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ReadTimeout);
        var response = new TaskCompletionSource<JsonDocument>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (!pendingResponses.TryAdd(requestId, response))
        {
            throw new InvalidOperationException($"Duplicate app-server request id {requestId}.");
        }

        try
        {
            await WriteAsync(request, timeout.Token);
            var result = await response.Task.WaitAsync(timeout.Token);
            if (result.RootElement.TryGetProperty("error", out var error))
            {
                var message = error.TryGetProperty("message", out var errorMessage)
                    ? errorMessage.GetString()
                    : "Unknown Codex app-server error.";
                result.Dispose();
                throw new InvalidOperationException(message);
            }

            return result;
        }
        finally
        {
            pendingResponses.TryRemove(requestId, out _);
        }
    }

    private async Task WriteAsync(object message, CancellationToken cancellationToken)
    {
        await writeLock.WaitAsync(cancellationToken);
        try
        {
            if (process is null)
            {
                throw new InvalidOperationException("Codex app-server is not running.");
            }

            await process.StandardInput.WriteLineAsync(
                JsonSerializer.Serialize(message).AsMemory(),
                cancellationToken);
            await process.StandardInput.FlushAsync(cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }

    private async Task ReadMessagesAsync(Process runningProcess)
    {
        try
        {
            while (await runningProcess.StandardOutput.ReadLineAsync() is { } line)
            {
                var message = JsonDocument.Parse(line);
                var root = message.RootElement;
                if (root.TryGetProperty("id", out var id))
                {
                    if (pendingResponses.TryRemove(id.GetInt32(), out var response))
                    {
                        response.TrySetResult(message);
                    }
                    else
                    {
                        message.Dispose();
                    }

                    continue;
                }

                if (root.TryGetProperty("method", out var method)
                    && method.GetString() == "account/rateLimits/updated")
                {
                    _ = NotifyUsageUpdatedAsync();
                }

                message.Dispose();
            }

            FailPendingResponses(new InvalidOperationException("Codex app-server closed before replying."));
        }
        catch (Exception exception)
        {
            FailPendingResponses(exception);
        }
        finally
        {
            if (ReferenceEquals(process, runningProcess))
            {
                process = null;
            }

            runningProcess.Dispose();
        }
    }

    private async Task NotifyUsageUpdatedAsync()
    {
        var handlers = UsageUpdated;
        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList().Cast<Func<Task>>())
        {
            try
            {
                await handler();
            }
            catch
            {
            }
        }
    }

    private void FailPendingResponses(Exception exception)
    {
        foreach (var pendingResponse in pendingResponses)
        {
            if (pendingResponses.TryRemove(pendingResponse.Key, out var response))
            {
                response.TrySetException(exception);
            }
        }
    }

    private Process StartAppServer()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("Codex app-server did not start.");
    }


    private static double? ReadWeeklyUsedPercentage(JsonElement result)
    {
        if (result.TryGetProperty("rateLimitsByLimitId", out var limitsById)
            && limitsById.ValueKind == JsonValueKind.Object
            && limitsById.TryGetProperty("codex", out var codexLimits))
        {
            var usedPercentage = ReadWeeklyWindow(codexLimits);
            if (usedPercentage is not null)
            {
                return usedPercentage;
            }
        }

        if (result.TryGetProperty("rateLimits", out var rateLimits)
            && rateLimits.ValueKind == JsonValueKind.Object
            && IsLegacyCodexBucket(rateLimits))
        {
            return ReadWeeklyWindow(rateLimits);
        }

        return null;
    }

    private static double? ReadWeeklyWindow(JsonElement rateLimits)
    {
        foreach (var windowName in new[] { "primary", "secondary" })
        {
            if (!rateLimits.TryGetProperty(windowName, out var window)
                || window.ValueKind != JsonValueKind.Object
                || !window.TryGetProperty("windowDurationMins", out var duration)
                || !duration.TryGetInt32(out var durationMinutes)
                || durationMinutes != WeeklyWindowDurationMinutes
                || !window.TryGetProperty("usedPercent", out var usedPercentage)
                || !usedPercentage.TryGetDouble(out var value))
            {
                continue;
            }

            return value;
        }

        return null;
    }

    private static bool IsLegacyCodexBucket(JsonElement rateLimits)
    {
        if (!rateLimits.TryGetProperty("limitId", out var limitId)
            || limitId.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        return limitId.ValueKind == JsonValueKind.String
            && limitId.GetString() == "codex";
    }

    private static void StopAppServer(Process process)
    {
        try
        {
            process.StandardInput.Close();
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (IOException)
        {
        }
    }
}
