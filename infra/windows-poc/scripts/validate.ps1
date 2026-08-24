param(
    [string]$EnvironmentFile = ".env.poc"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "common.ps1")

$resolvedEnvironment = Resolve-PocEnvironmentFile -Path $EnvironmentFile
Assert-DockerAvailable
Assert-PocEnvironment -Path $resolvedEnvironment | Out-Null
Invoke-PocCompose -EnvironmentFile $resolvedEnvironment -Arguments @("config", "--quiet")

Write-Host "Windows PoC Compose configuration is valid; required secrets were supplied without being printed."
