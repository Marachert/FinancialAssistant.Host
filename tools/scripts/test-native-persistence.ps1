param([Parameter(Mandatory)][string] $ArchivePath)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows) { throw 'Native persistence qualification requires Windows, not Docker/WSL.' }
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$archive = [IO.Path]::GetFullPath($ArchivePath)
if (-not (Test-Path -LiteralPath $archive -PathType Leaf)) { throw 'PostgreSQL archive is missing.' }
$manifest = Get-Content -LiteralPath (Join-Path $repo 'infra/windows-native/component-manifest.json') -Raw | ConvertFrom-Json
$package = @($manifest.packages | Where-Object id -eq 'postgresql')
if ($package.Count -ne 1 -or $package[0].hashAlgorithm -ne 'SHA256' -or
    (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $package[0].hash) { throw 'PostgreSQL archive digest mismatch.' }
$root = Join-Path $repo '.codex-runtime/persistence-qualification'
for ($cursor = $root; $cursor; $cursor = Split-Path -Parent $cursor) {
    if ((Test-Path -LiteralPath $cursor) -and ((Get-Item -LiteralPath $cursor).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'Linked qualification path is forbidden.'
    }
}
$dotnet = (Get-Command dotnet -CommandType Application | Select-Object -First 1).Source
$pwsh = (Get-Command pwsh -CommandType Application | Select-Object -First 1).Source
$scenarios = @('Foundation','Identity','ProfileCategory','FinancialRecords','Intake','ReceiptAi','Projections','Insights','Audit')
$results = @($scenarios | ForEach-Object { [ordered]@{scenario=$_;status='not-run';evidence=$null} })
$run = Join-Path $root ([guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $run
$identity = [Security.Principal.WindowsIdentity]::GetCurrent().User
$acl = [Security.AccessControl.DirectorySecurity]::new()
$acl.SetAccessRuleProtection($true, $false); $acl.SetOwner($identity)
$acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($identity, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow'))
Set-Acl -LiteralPath $run -AclObject $acl
$startedAt = [datetime]::UtcNow
$passed = $false
$buildPassed = $false
$sourceHead = $null
$sourceDirty = $null
Push-Location $repo
try {
    $sourceHead = & git rev-parse HEAD
    if ($LASTEXITCODE -ne 0 -or $sourceHead -notmatch '^[a-f0-9]{40}$') { throw 'Cannot identify qualification source head.' }
    $sourceChanges = @(& git status --porcelain --untracked-files=normal)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot identify qualification source changes.' }
    $sourceDirty = $sourceChanges.Count -ne 0
    # Build this checkout, never silently qualify stale --no-build assemblies.
    $build = Start-Process -FilePath $dotnet -ArgumentList @('build','FinancialAssistant.Backend.sln','--no-restore','--configuration','Release','-m:1','/p:UseSharedCompilation=false','/nodeReuse:false') -WindowStyle Hidden -PassThru -WorkingDirectory $repo -RedirectStandardOutput (Join-Path $run 'build.stdout.log') -RedirectStandardError (Join-Path $run 'build.stderr.log')
    $buildStarted = $build.StartTime.ToUniversalTime()
    if (-not $build.WaitForExit(1200000)) {
        if ($build.StartTime.ToUniversalTime() -ne $buildStarted -or $build.Path -ne $dotnet) { throw 'Build process ownership changed; refusing termination.' }
        $build.Kill($true)
        if (-not $build.WaitForExit(15000)) { throw 'Owned build did not stop after its deadline.' }
        throw 'Persistence qualification build timed out.'
    }
    if ($build.ExitCode -ne 0) { throw 'Persistence qualification build failed; see private build logs. Restore dependencies first.' }
    $buildPassed = $true
    foreach ($entry in $results) {
        $entry.status = 'failed'
        $target = Join-Path $run ($entry.scenario.ToLowerInvariant() + '.json')
        $scenarioStarted = [datetime]::UtcNow
        & $pwsh -NoProfile -NonInteractive -File (Join-Path $PSScriptRoot 'test-owned-postgres.ps1') -ArchivePath $archive -Scenario $entry.scenario -ResultFile $target
        if ($LASTEXITCODE -ne 0) { throw "Persistence scenario failed: $($entry.scenario). Remaining scenarios are not-run." }
        if (-not (Test-Path -LiteralPath $target -PathType Leaf)) { throw 'Scenario evidence missing; console success is insufficient.' }
        $evidence = Get-Content -LiteralPath $target -Raw | ConvertFrom-Json
        $fixture = [IO.Path]::GetFullPath((Join-Path $repo $evidence.fixture))
        $directory = Get-Item -LiteralPath $fixture -ErrorAction Stop
        $nonce = [guid]::Empty
        if ($evidence.passed -isnot [bool] -or $evidence.cleanupSucceeded -isnot [bool] -or
            $evidence.installed -isnot [bool] -or $evidence.releaseReady -isnot [bool] -or
            $evidence.scenario -isnot [string] -or $evidence.archiveDigest -isnot [string] -or $evidence.fixture -isnot [string] -or
            $evidence.passed -cne $true -or $evidence.cleanupSucceeded -cne $true -or
            $evidence.installed -cne $false -or $evidence.releaseReady -cne $false -or
            $evidence.scenario -cne $entry.scenario -or $evidence.archiveDigest -cne $package[0].hash -or
            ($entry.scenario -eq 'Foundation' -and ($evidence.interruptedTransactionRecovered -isnot [bool] -or $evidence.interruptedTransactionRecovered -cne $true)) -or
            [datetime]$evidence.verifiedAt -lt $scenarioStarted -or [datetime]$evidence.verifiedAt -gt [datetime]::UtcNow -or
            -not $directory.PSIsContainer -or -not [guid]::TryParseExact($directory.Name,'N',[ref]$nonce) -or
            $directory.Parent.FullName -ne (Join-Path $repo '.codex-runtime/persistence-test') -or
            ($directory.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
            (Test-Path -LiteralPath (Join-Path $fixture 'data/postmaster.pid')) -or
            (Test-Path -LiteralPath (Join-Path $fixture 'fixture-password.txt'))) { throw 'Scenario evidence or checked cleanup is invalid.' }
        $entry.evidence = [IO.Path]::GetRelativePath($repo,$target)
        $entry.status = 'passed'
    }
    $passed = @($results | Where-Object status -ne 'passed').Count -eq 0
    if (-not $passed) { throw 'Incomplete persistence matrix is not a pass.' }
} finally {
    Pop-Location
    $summary = [ordered]@{schemaVersion=1;passed=$passed;buildPassed=$buildPassed;requiredScenarios=$scenarios;results=$results;
        sourceHead=$sourceHead;sourceDirty=$sourceDirty;startedAt=$startedAt.ToString('o');verifiedAt=[datetime]::UtcNow.ToString('o');archiveDigest=$package[0].hash;
        installed=$false;releaseReady=$false;paidProviderInvocations=0}
    $stream = [IO.File]::Open((Join-Path $run 'summary.json'), [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $bytes = [Text.UTF8Encoding]::new($false).GetBytes(($summary | ConvertTo-Json -Depth 8)); $stream.Write($bytes) }
    finally { $stream.Dispose() }
    Write-Output ('Persistence matrix evidence: ' + [IO.Path]::GetRelativePath($repo,(Join-Path $run 'summary.json')))
}
Write-Output 'All nine owned PostgreSQL scenarios passed initial/crash/restart and checked cleanup. Installed Windows qualification remains pending.'
