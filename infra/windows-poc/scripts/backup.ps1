param(
    [Parameter(Mandatory)][string]$BackupRoot,
    [string]$EnvironmentFile = ".env.poc"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "common.ps1")

$resolvedEnvironment = Resolve-PocEnvironmentFile -Path $EnvironmentFile
$values = Assert-PocEnvironment -Path $resolvedEnvironment
Assert-DockerAvailable
$resolvedBackupRoot = Resolve-BackupRoot -Path $BackupRoot
$timestamp = [DateTimeOffset]::UtcNow.ToString("yyyyMMddTHHmmssZ")
$backupDirectory = Join-Path $resolvedBackupRoot $timestamp
[System.IO.Directory]::CreateDirectory($backupDirectory) | Out-Null
$alpineImage = $values["ALPINE_IMAGE"]
if ([string]::IsNullOrWhiteSpace($alpineImage)) {
    throw "ALPINE_IMAGE is required for volume backup."
}

$snapshotRepository = '{"type":"fs","settings":{"location":"/usr/share/elasticsearch/snapshots","compress":true}}'
$snapshotName = "pre-volume-$($timestamp.ToLowerInvariant())"
Invoke-PocCompose -EnvironmentFile $resolvedEnvironment -Arguments @(
    "exec", "-T", "elasticsearch", "curl", "--fail", "--silent", "--show-error",
    "--request", "PUT", "--header", "Content-Type: application/json",
    "--data", $snapshotRepository, "http://localhost:9200/_snapshot/fa-poc-backups"
)
Invoke-PocCompose -EnvironmentFile $resolvedEnvironment -Arguments @(
    "exec", "-T", "elasticsearch", "curl", "--fail", "--silent", "--show-error",
    "--request", "PUT", "http://localhost:9200/_snapshot/fa-poc-backups/$snapshotName?wait_for_completion=true"
)

$stopped = $false
try {
    Invoke-PocCompose -EnvironmentFile $resolvedEnvironment -Arguments @("stop")
    $stopped = $true
    foreach ($volume in $script:DurableVolumes) {
        $archive = "$volume.tar.gz"
        & docker run --rm `
            --volume "${volume}:/source:ro" `
            --volume "${backupDirectory}:/backup" `
            $alpineImage sh -c "cd /source && tar -czf /backup/$archive ."
        if ($LASTEXITCODE -ne 0) {
            throw "Backup failed for the allowlisted volume $volume."
        }
    }

    $manifest = [ordered]@{
        createdAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
        elasticsearchSnapshot = $snapshotName
        volumes = $script:DurableVolumes
    }
    [System.IO.File]::WriteAllText(
        (Join-Path $backupDirectory "manifest.json"),
        ($manifest | ConvertTo-Json -Depth 3),
        [System.Text.UTF8Encoding]::new($false))
}
finally {
    if ($stopped) {
        Invoke-PocCompose -EnvironmentFile $resolvedEnvironment -Arguments @("up", "--detach", "--wait")
    }
}

Write-Host "Backup completed at $backupDirectory. No environment values were written to the manifest."
