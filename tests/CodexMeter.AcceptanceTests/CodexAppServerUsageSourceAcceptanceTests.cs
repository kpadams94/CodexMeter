using CodexMeter;
using System.IO;
using System.Text.Json;

namespace CodexMeter.AcceptanceTests;

public sealed partial class ApplicationSessionAcceptanceTests
{
    [Theory]
    [InlineData("multiBucket")]
    [InlineData("legacyNullMetadata")]
    public void Production_session_reads_the_weekly_window_through_codex_app_server(string responseShape)
    {
        StaThread.Run(() =>
        {
            var transcriptPath = Path.Combine(Path.GetTempPath(), $"codex-meter-{Guid.NewGuid():N}.jsonl");
            var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "FakeCodexAppServer.ps1");
            var widget = new RecordingWidgetShell();
            var usageSource = new CodexAppServerUsageSource(
                "powershell.exe",
                "-NoProfile",
                "-NonInteractive",
                "-File",
                fixturePath,
                "-TranscriptPath",
                transcriptPath,
                "-ResponseShape",
                responseShape);
            var adapters = new ApplicationSessionAdapters(
                usageSource,
                new ControlledClock(),
                new InMemoryUsageStateStore(),
                new ControlledDesktopState(),
                new RecordingNotificationSink(),
                widget);

            try
            {
                new ApplicationSession(adapters).StartAsync().GetAwaiter().GetResult();
                var requests = File.ReadAllLines(transcriptPath)
                    .Select(line => JsonDocument.Parse(line))
                    .ToArray();

                Assert.Equal(37, Assert.IsType<UsageState>(widget.LastState).Remaining.Value);
                Assert.Equal("initialize", requests[0].RootElement.GetProperty("method").GetString());
                Assert.Equal("initialized", requests[1].RootElement.GetProperty("method").GetString());
                Assert.Equal("account/rateLimits/read", requests[2].RootElement.GetProperty("method").GetString());
                Assert.DoesNotContain(requests, request =>
                    request.RootElement.GetProperty("method").GetString()!.Contains("login", StringComparison.Ordinal));

                foreach (var request in requests)
                {
                    request.Dispose();
                }
            }
            finally
            {
                File.Delete(transcriptPath);
            }
        });
    }
}
