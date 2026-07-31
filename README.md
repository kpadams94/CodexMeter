# CodexMeter

A tiny Windows widget for monitoring remaining weekly Codex usage.

The current application is a .NET 8 WPF Quiet Card that launches with a controlled
47% sample. The application-session boundary accepts controlled usage, time,
persistence, desktop-state, notification, and widget adapters so subsequent work
can exercise complete sessions without account access or Windows side effects.

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
