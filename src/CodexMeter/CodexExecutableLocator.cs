using System.IO;

namespace CodexMeter;

internal static class CodexExecutableLocator
{
    public static string Resolve() =>
        Resolve(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    internal static string Resolve(string localApplicationDataPath)
    {
        var binDirectory = Path.Combine(localApplicationDataPath, "OpenAI", "Codex", "bin");

        try
        {
            return Directory
                .EnumerateFiles(binDirectory, "codex.exe", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault()
                ?? "codex";
        }
        catch (DirectoryNotFoundException)
        {
            return "codex";
        }
        catch (UnauthorizedAccessException)
        {
            return "codex";
        }
        catch (IOException)
        {
            return "codex";
        }
    }
}
