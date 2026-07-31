using System.IO;
using System.Text.Json;

namespace CodexMeter;

public sealed class LocalUsageStateStore : IUsageStateStore
{
    private const string FileName = "usage-state.json";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly string stateFilePath;

    public LocalUsageStateStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodexMeter"))
    {
    }

    internal LocalUsageStateStore(string applicationDataDirectory)
    {
        stateFilePath = Path.Combine(applicationDataDirectory, FileName);
    }

    public async Task<UsageState?> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(stateFilePath);
            var persistedState = await JsonSerializer.DeserializeAsync<PersistedUsageState>(
                stream,
                SerializerOptions,
                cancellationToken: cancellationToken);
            if (persistedState?.RemainingPercentage is not double remainingPercentage
                || remainingPercentage is < 0 or > 100
                || persistedState.CheckedAt is not DateTimeOffset checkedAt)
            {
                return null;
            }

            return new UsageState(RemainingPercentage.From(remainingPercentage), checkedAt);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task SaveAsync(UsageState state, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(stateFilePath)!);
        await using var stream = File.Create(stateFilePath);
        await JsonSerializer.SerializeAsync(
            stream,
            new PersistedUsageState(state.Remaining.Value, state.CheckedAt),
            SerializerOptions,
            cancellationToken: cancellationToken);
    }

    private sealed record PersistedUsageState(double? RemainingPercentage, DateTimeOffset? CheckedAt);
}
