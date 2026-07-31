# CodexMeter

A tiny Windows widget for monitoring remaining weekly Codex usage.

The current application is a .NET 8 WPF Quiet Card that reads the signed-in
Codex account's weekly rate-limit window through `codex app-server`. It displays
the remaining percentage at startup and refreshes on left-click or through the
right-click **Refresh Now** command. Failed reads remain silent and preserve the
last successful value.

## Requirements

- Windows 10 or later
- .NET 8 SDK with the Windows Desktop workload

## Run

```powershell
dotnet run --project src/CodexMeter/CodexMeter.csproj
```

## Test

```powershell
dotnet test CodexMeter.sln
```
