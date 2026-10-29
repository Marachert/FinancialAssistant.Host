param([Parameter(Mandatory)][string] $ArchivePath,
    [ValidateSet('Foundation','Identity','ProfileCategory','FinancialRecords','Intake','ReceiptAi','Projections','Insights')][string] $Scenario = 'Foundation')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows) { throw 'This fixture uses the Windows PostgreSQL archive, not Docker/WSL.' }
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$archive = [IO.Path]::GetFullPath($ArchivePath)
if ($archive.Contains('"') -or -not (Test-Path -LiteralPath $archive -PathType Leaf)) { throw 'Invalid archive path.' }
$manifest = Get-Content -LiteralPath (Join-Path $repo 'infra/windows-native/component-manifest.json') -Raw | ConvertFrom-Json
$package = @($manifest.packages | Where-Object id -eq 'postgresql')
if ($package.Count -ne 1 -or $package[0].hashAlgorithm -ne 'SHA256' -or
    (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $package[0].hash) { throw 'PostgreSQL archive digest mismatch.' }
$root = Join-Path $repo '.codex-runtime/persistence-test'
$ancestor = $root
while ($ancestor -and $ancestor.StartsWith($repo, [StringComparison]::OrdinalIgnoreCase)) {
    if ((Test-Path -LiteralPath $ancestor) -and ((Get-Item -LiteralPath $ancestor).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Fixture path contains a reparse point.' }
    $ancestor = Split-Path -Parent $ancestor
}
$run = Join-Path $root ([guid]::NewGuid().ToString('N'))
$data = Join-Path $run 'data'
$binaries = Join-Path $run 'pgsql/bin'
$passwordFile = Join-Path $run 'fixture-password.txt'
$null = New-Item -ItemType Directory -Path $run
$started = $false
$runStarted = [datetime]::UtcNow
$passed = $false
$cleanupSucceeded = $false
$saved = @{}
foreach ($name in @('FA_PERSISTENCE_TEST_PASSWORD','FA_PERSISTENCE_TEST_PORT','FA_PERSISTENCE_TEST_DIRECTORY','FA_PERSISTENCE_TEST_MODE')) {
    $saved[$name] = [Environment]::GetEnvironmentVariable($name)
}
function Run-Local([string] $Executable, [string[]] $Arguments, [string] $Label) {
    foreach ($argument in $Arguments) { if ($argument.Contains('"') -or $argument.Contains("`n") -or $argument.Contains("`r")) { throw 'Unsafe process argument.' } }
    $quoted = @($Arguments | ForEach-Object { '"' + $_ + '"' })
    $process = Start-Process -FilePath $Executable -ArgumentList $quoted -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $run "$Label.stdout.log") -RedirectStandardError (Join-Path $run "$Label.stderr.log")
    if (-not $process.WaitForExit(120000)) {
        $process.Kill()
        $process.WaitForExit()
        throw "Owned fixture process timeout: $Label"
    }
    if ($process.ExitCode -ne 0) { throw "Owned fixture process failed: $Label (exit $($process.ExitCode)). Details retained locally, not printed." }
}
try {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($archive)
    try {
        foreach ($entry in $zip.Entries) {
            if ($entry.FullName -notmatch '^pgsql/(bin|lib|share)/' -or $entry.FullName.EndsWith('/')) { continue }
            $target = [IO.Path]::GetFullPath((Join-Path $run $entry.FullName))
            if (-not $target.StartsWith($run + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
                $entry.FullName.Contains('..') -or $entry.FullName.Contains(':')) { throw 'Archive entry escapes owned fixture.' }
            $null = New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $false)
        }
    } finally { $zip.Dispose() }
    # No installer, registry, SCM, application-control bypass or trust changes.
    $password = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    [IO.File]::WriteAllText($passwordFile, $password, [Text.UTF8Encoding]::new($false))
    Run-Local (Join-Path $binaries 'initdb.exe') @('-D',$data,'-U','fa_test_admin','--auth-host=scram-sha-256','--auth-local=scram-sha-256','--pwfile',$passwordFile,'--encoding=UTF8','--locale=C') 'initdb'
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $port = $listener.LocalEndpoint.Port
    $listener.Stop()
    if ($port -lt 49152) { throw 'Fixture port is outside the dynamic range.' }
    [IO.File]::AppendAllText((Join-Path $data 'postgresql.conf'), "`nlisten_addresses='127.0.0.1'`nport=$port`nlogging_collector=off`nlog_statement='none'`nlog_min_error_statement='panic'`n", [Text.UTF8Encoding]::new($false))
    $pgctl = Join-Path $binaries 'pg_ctl.exe'
    Run-Local $pgctl @('-D',$data,'-l',(Join-Path $run 'postgres.log'),'-w','start') 'start'
    $started = $true
    $env:FA_PERSISTENCE_TEST_PASSWORD = $password
    $env:FA_PERSISTENCE_TEST_PORT = [string]$port
    $env:FA_PERSISTENCE_TEST_DIRECTORY = $data
    $env:FA_PERSISTENCE_TEST_MODE = 'initial'
    $project = Join-Path $repo $(if ($Scenario -eq 'Identity') {
        'backend/services/identity/FinancialAssistant.Identity.Persistence.Integration/FinancialAssistant.Identity.Persistence.Integration.csproj'
    } elseif ($Scenario -eq 'ProfileCategory') {
        'backend/services/profile/FinancialAssistant.ProfileCategory.Persistence.Integration/FinancialAssistant.ProfileCategory.Persistence.Integration.csproj'
    } elseif ($Scenario -eq 'FinancialRecords') {
        'backend/services/income/FinancialAssistant.FinancialRecords.Persistence.Integration/FinancialAssistant.FinancialRecords.Persistence.Integration.csproj'
    } elseif ($Scenario -eq 'Intake') {
        'backend/services/transaction-intake/FinancialAssistant.Intake.Persistence.Integration/FinancialAssistant.Intake.Persistence.Integration.csproj'
    } elseif ($Scenario -eq 'ReceiptAi') {
        'backend/services/receipt-processing/FinancialAssistant.ReceiptAi.Persistence.Integration/FinancialAssistant.ReceiptAi.Persistence.Integration.csproj'
    } elseif ($Scenario -eq 'Projections') {
        'backend/services/financial-summary/FinancialAssistant.Projections.Persistence.Integration/FinancialAssistant.Projections.Persistence.Integration.csproj'
    } elseif ($Scenario -eq 'Insights') {
        'backend/services/financial-score/FinancialAssistant.Insights.Persistence.Integration/FinancialAssistant.Insights.Persistence.Integration.csproj'
    } else {
        'backend/shared/persistence/FinancialAssistant.Shared.Persistence.Integration/FinancialAssistant.Shared.Persistence.Integration.csproj'
    })
    Push-Location $repo
    try {
        & dotnet run --project $project --no-build --configuration Release
        if ($LASTEXITCODE -ne 0) { throw 'Initial PostgreSQL integration failed.' }
        Run-Local $pgctl @('-D',$data,'-m','immediate','-w','stop') 'crash-stop'
        $started = $false
        Run-Local $pgctl @('-D',$data,'-l',(Join-Path $run 'postgres-restart.log'),'-w','start') 'restart'
        $started = $true
        $env:FA_PERSISTENCE_TEST_MODE = 'restart'
        & dotnet run --project $project --no-build --configuration Release
        if ($LASTEXITCODE -ne 0) { throw 'Restart PostgreSQL integration failed.' }
        $passed = $true
    } finally { Pop-Location }
} finally {
    try {
        $pidFile = Join-Path $data 'postmaster.pid'
        if ($started -or (Test-Path -LiteralPath $pidFile)) {
            if (-not (Test-Path -LiteralPath $pidFile)) { throw 'Owned PostgreSQL PID file missing; no foreign process will be stopped.' }
            $serverProcess = Get-Process -Id ([int](Get-Content -LiteralPath $pidFile -TotalCount 1)) -ErrorAction Stop
            if ($serverProcess.Path -ne (Join-Path $binaries 'postgres.exe') -or $serverProcess.StartTime.ToUniversalTime() -lt $runStarted) { throw 'PostgreSQL process ownership mismatch; refusing cleanup.' }
            Run-Local (Join-Path $binaries 'pg_ctl.exe') @('-D',$data,'-m','fast','-w','stop') 'cleanup-stop'
        }
        $cleanupSucceeded = $true
    } catch {
        $passed = $false
        throw
    } finally {
        foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name, $saved[$name], 'Process') }
        if (Test-Path -LiteralPath $passwordFile) { Remove-Item -LiteralPath $passwordFile -Force }
        @{passed=$passed;cleanupSucceeded=$cleanupSucceeded;scenario=$Scenario;installed=$false;releaseReady=$false;fixture=[IO.Path]::GetRelativePath($repo,$run);archiveDigest=$package[0].hash;verifiedAt=[datetime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'result.json') -Encoding utf8NoBOM
    }
}
Write-Output 'Owned local PostgreSQL fixture passed and stopped; installed/runtime qualification remains pending.'
