param(
    [string]$EnvironmentFile = ".env.poc",
    [switch]$SkipPull
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "common.ps1")

$resolvedEnvironment = Resolve-PocEnvironmentFile -Path $EnvironmentFile
& (Join-Path $PSScriptRoot "validate.ps1") -EnvironmentFile $resolvedEnvironment
if (-not $SkipPull) {
    Invoke-PocCompose -EnvironmentFile $resolvedEnvironment -Arguments @("pull", "--ignore-buildable")
}
Invoke-PocCompose -EnvironmentFile $resolvedEnvironment -Arguments @("build", "--pull")
Invoke-PocCompose -EnvironmentFile $resolvedEnvironment -Arguments @("up", "--detach", "--wait")
Invoke-PocCompose -EnvironmentFile $resolvedEnvironment -Arguments @("ps")

Write-Host "Financial Assistant PoC stack is healthy behind the reverse proxy."
