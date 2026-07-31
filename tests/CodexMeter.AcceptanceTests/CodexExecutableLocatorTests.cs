using CodexMeter;
using System.IO;

namespace CodexMeter.AcceptanceTests;

public sealed class CodexExecutableLocatorTests
{
    [Fact]
    public void User_level_codex_binary_is_preferred_over_the_execution_alias()
    {
        var localAppData = Path.Combine(Path.GetTempPath(), $"codex-meter-locator-{Guid.NewGuid():N}");
        var olderDirectory = Path.Combine(localAppData, "OpenAI", "Codex", "bin", "older");
        var currentDirectory = Path.Combine(localAppData, "OpenAI", "Codex", "bin", "current");
        var olderExecutable = Path.Combine(olderDirectory, "codex.exe");
        var currentExecutable = Path.Combine(currentDirectory, "codex.exe");

        try
        {
            Directory.CreateDirectory(olderDirectory);
            Directory.CreateDirectory(currentDirectory);
            File.WriteAllText(olderExecutable, string.Empty);
            File.WriteAllText(currentExecutable, string.Empty);
            File.SetLastWriteTimeUtc(olderExecutable, new DateTime(2026, 1, 1));
            File.SetLastWriteTimeUtc(currentExecutable, new DateTime(2026, 7, 31));

            Assert.Equal(currentExecutable, CodexExecutableLocator.Resolve(localAppData));
        }
        finally
        {
            Directory.Delete(localAppData, recursive: true);
        }
    }
}
