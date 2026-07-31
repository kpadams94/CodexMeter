using System.IO;
using System.Text.Json;
using CodexMeter;

namespace CodexMeter.AcceptanceTests;

public sealed class LocalUsageStateStoreAcceptanceTests
{
    [Fact]
    public async Task Saves_only_the_remaining_percentage_and_successful_check_time()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var store = new LocalUsageStateStore(directory);
            var state = new UsageState(
                RemainingPercentage.From(47),
                new DateTimeOffset(2026, 7, 31, 18, 0, 0, TimeSpan.Zero));

            await store.SaveAsync(state, CancellationToken.None);
            var stateFile = Path.Combine(directory, "usage-state.json");
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(stateFile));

            Assert.Equal(2, document.RootElement.EnumerateObject().Count());
            Assert.Equal(47, document.RootElement.GetProperty("remainingPercentage").GetDouble());
            Assert.Equal(
                state.CheckedAt,
                document.RootElement.GetProperty("checkedAt").GetDateTimeOffset());
            Assert.Equal(state, await store.LoadAsync(CancellationToken.None));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Missing_or_corrupt_saved_state_is_ignored()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var store = new LocalUsageStateStore(directory);

            Assert.Null(await store.LoadAsync(CancellationToken.None));

            var stateFile = Path.Combine(directory, "usage-state.json");
            await File.WriteAllTextAsync(stateFile, "not json");

            Assert.Null(await store.LoadAsync(CancellationToken.None));

            await File.WriteAllTextAsync(stateFile, "{}");

            Assert.Null(await store.LoadAsync(CancellationToken.None));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CodexMeter.AcceptanceTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
