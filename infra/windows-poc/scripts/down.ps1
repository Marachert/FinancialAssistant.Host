param(
    [string]$EnvironmentFile = ".env.poc"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "common.ps1")

$resolvedEnvironment = Resolve-PocEnvironmentFile -Path $EnvironmentFile
Assert-DockerAvailable
Invoke-PocCompose -EnvironmentFile $resolvedEnvironment -Arguments @("down", "--remove-orphans")

Write-Host "Financial Assistant PoC stack is stopped; durable volumes were retained."
