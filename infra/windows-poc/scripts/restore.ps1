param(
    [Parameter(Mandatory)][string]$BackupDirectory,
    [Parameter(Mandatory)][switch]$ConfirmRestore,
    [string]$EnvironmentFile = ".env.poc"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "common.ps1")

if (-not $ConfirmRestore) {
    throw "Restore replaces the eight fixed PoC volumes. Pass -ConfirmRestore explicitly."
}
$resolvedEnvironment = Resolve-PocEnvironmentFile -Path $EnvironmentFile
$values = Assert-PocEnvironment -Path $resolvedEnvironment
Assert-DockerAvailable
$resolvedBackup = (Resolve-Path -LiteralPath $BackupDirectory).Path
$alpineImage = $values["ALPINE_IMAGE"]
if ([string]::IsNullOrWhiteSpace($alpineImage)) {
    throw "ALPINE_IMAGE is required for volume restore."
}
foreach ($volume in $script:DurableVolumes) {
    $archivePath = Join-Path $resolvedBackup "$volume.tar.gz"
    if (-not (Test-Path -LiteralPath $archivePath -PathType Leaf)) {
        throw "Required backup archive is missing: $archivePath"
    }
}

Invoke-PocCompose -EnvironmentFile $resolvedEnvironment -Arguments @("down", "--remove-orphans")
foreach ($volume in $script:DurableVolumes) {
    & docker volume create $volume | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Could not create the allowlisted volume $volume."
    }
    $archive = "$volume.tar.gz"
    & docker run --rm `
        --volume "${volume}:/target" `
        --volume "${resolvedBackup}:/backup:ro" `
        $alpineImage sh -c "find /target -mindepth 1 -maxdepth 1 -exec rm -rf '{}' '+' && tar -xzf /backup/$archive -C /target"
    if ($LASTEXITCODE -ne 0) {
        throw "Restore failed for the allowlisted volume $volume."
    }
}
Invoke-PocCompose -EnvironmentFile $resolvedEnvironment -Arguments @("up", "--detach", "--wait")

Write-Host "Restore completed from $resolvedBackup and the PoC stack is healthy."
