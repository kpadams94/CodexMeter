param(
    [Parameter(Mandatory = $true)]
    [string]$TranscriptPath,

    [Parameter(Mandatory = $true)]
    [ValidateSet('multiBucket', 'legacyNullMetadata')]
    [string]$ResponseShape
)

$initialize = [Console]::In.ReadLine()
Add-Content -LiteralPath $TranscriptPath -Value $initialize
[Console]::Out.WriteLine('{"id":1,"result":{"userAgent":"fake-codex-app-server"}}')

$initialized = [Console]::In.ReadLine()
Add-Content -LiteralPath $TranscriptPath -Value $initialized

$rateLimitsRead = [Console]::In.ReadLine()
Add-Content -LiteralPath $TranscriptPath -Value $rateLimitsRead

if ($ResponseShape -eq 'legacyNullMetadata') {
    [Console]::Out.WriteLine('{"id":2,"result":{"rateLimits":{"limitId":null,"primary":{"usedPercent":11,"windowDurationMins":300,"resetsAt":1},"secondary":{"usedPercent":63,"windowDurationMins":10080,"resetsAt":2}},"rateLimitsByLimitId":null}}')
}
else {
    [Console]::Out.WriteLine('{"id":2,"result":{"rateLimits":{"limitId":"codex","primary":{"usedPercent":11,"windowDurationMins":300,"resetsAt":1},"secondary":{"usedPercent":9,"windowDurationMins":10080,"resetsAt":2}},"rateLimitsByLimitId":{"codex":{"limitId":"codex","primary":{"usedPercent":11,"windowDurationMins":300,"resetsAt":1},"secondary":{"usedPercent":63,"windowDurationMins":10080,"resetsAt":2}},"codex_other":{"limitId":"codex_other","primary":{"usedPercent":12,"windowDurationMins":10080,"resetsAt":3},"secondary":null}}}}')
}
