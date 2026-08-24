param(
    [string]$EnvironmentFile = ".env.poc"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "common.ps1")

$resolvedEnvironment = Resolve-PocEnvironmentFile -Path $EnvironmentFile
$values = Assert-PocEnvironment -Path $resolvedEnvironment
Assert-DockerAvailable
Invoke-PocCompose -EnvironmentFile $resolvedEnvironment -Arguments @("config", "--quiet")
Invoke-PocCompose -EnvironmentFile $resolvedEnvironment -Arguments @("ps")

$port = if ($values.ContainsKey("POC_HTTP_PORT")) { $values["POC_HTTP_PORT"] } else { "8080" }
Invoke-WebRequest "http://127.0.0.1:$port/reverse-proxy-health" -UseBasicParsing | Out-Null
Invoke-WebRequest "http://127.0.0.1:$port/health" -UseBasicParsing | Out-Null

Write-Host "Reverse proxy and Public API Gateway health checks passed."
