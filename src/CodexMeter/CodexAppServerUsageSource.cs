using System.Diagnostics;
using System.Text.Json;

namespace CodexMeter;

public sealed class CodexAppServerUsageSource : IUsageSource
{
    private const int WeeklyWindowDurationMinutes = 7 * 24 * 60;
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(15);
    private readonly string executablePath;
    private readonly IReadOnlyList<string> arguments;

    public CodexAppServerUsageSource()
        : this("codex", "app-server", "--listen", "stdio://")
    {
    }

    public CodexAppServerUsageSource(string executablePath, params string[] arguments)
    {
        this.executablePath = executablePath;
        this.arguments = arguments;
    }

    public async Task<double?> ReadWeeklyUsedPercentageAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ReadTimeout);

        using var process = StartAppServer();
        _ = process.StandardError.ReadToEndAsync(timeout.Token);

        try
        {
            await SendAsync(
                process,
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
                timeout.Token);
            using (await ReadResponseAsync(process, 1, timeout.Token))
            {
            }

            await SendAsync(
                process,
                new { method = "initialized", @params = new { } },
                timeout.Token);
            await SendAsync(
                process,
                new { method = "account/rateLimits/read", id = 2 },
                timeout.Token);

            using var response = await ReadResponseAsync(process, 2, timeout.Token);
            return ReadWeeklyUsedPercentage(response.RootElement.GetProperty("result"));
        }
        finally
        {
            process.StandardInput.Close();
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
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

    private static async Task SendAsync(
        Process process,
        object message,
        CancellationToken cancellationToken)
    {
        await process.StandardInput.WriteLineAsync(
            JsonSerializer.Serialize(message).AsMemory(),
            cancellationToken);
        await process.StandardInput.FlushAsync(cancellationToken);
    }

    private static async Task<JsonDocument> ReadResponseAsync(
        Process process,
        int expectedId,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await process.StandardOutput.ReadLineAsync(cancellationToken)
                ?? throw new InvalidOperationException("Codex app-server closed before replying.");
            var response = JsonDocument.Parse(line);
            var root = response.RootElement;

            if (!root.TryGetProperty("id", out var id) || id.GetInt32() != expectedId)
            {
                response.Dispose();
                continue;
            }

            if (root.TryGetProperty("error", out var error))
            {
                var message = error.TryGetProperty("message", out var errorMessage)
                    ? errorMessage.GetString()
                    : "Unknown Codex app-server error.";
                response.Dispose();
                throw new InvalidOperationException(message);
            }

            return response;
        }
    }

    private static double? ReadWeeklyUsedPercentage(JsonElement result)
    {
        if (result.TryGetProperty("rateLimitsByLimitId", out var limitsById)
            && limitsById.TryGetProperty("codex", out var codexLimits))
        {
            var usedPercentage = ReadWeeklyWindow(codexLimits);
            if (usedPercentage is not null)
            {
                return usedPercentage;
            }
        }

        if (result.TryGetProperty("rateLimits", out var rateLimits)
            && (!rateLimits.TryGetProperty("limitId", out var limitId)
                || limitId.GetString() == "codex"))
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
}
