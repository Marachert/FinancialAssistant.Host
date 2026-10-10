[CmdletBinding()]
param(
    [ValidatePattern('^[a-z0-9][a-z0-9-]{0,63}$')]
    [string] $CandidateId = 'development',
    [string] $ServiceId = 'all'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($CandidateId -match '^(con|prn|aux|nul|com[1-9]|lpt[1-9])$') { throw 'Reserved Windows candidate directory name.' }
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$runtimeRoot = [IO.Path]::GetFullPath((Join-Path $repository '.codex-runtime'))
$outputRoot = [IO.Path]::GetFullPath((Join-Path $runtimeRoot "native-hosts/$CandidateId"))
if (-not $outputRoot.StartsWith($runtimeRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Publish output must remain within the repository runtime directory.'
}
if (Test-Path -LiteralPath $outputRoot) { throw 'Candidate output already exists; select a new candidate ID rather than overwriting it.' }

$manifest = Get-Content -LiteralPath (Join-Path $repository 'infra/windows-native/component-manifest.json') -Raw | ConvertFrom-Json
$hosts = @($manifest.hosts | Where-Object { $ServiceId -eq 'all' -or $_.id -ceq $ServiceId })
if ($hosts.Count -eq 0 -or ($ServiceId -ne 'all' -and $hosts.Count -ne 1)) { throw 'Unknown or ambiguous native service ID.' }
foreach ($hostItem in $hosts) {
    $projectPath = [IO.Path]::GetFullPath((Join-Path $repository $hostItem.project))
    if (-not $projectPath.StartsWith($repository + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath $projectPath -PathType Leaf)) { throw 'Invalid native host project path.' }
}
& pwsh -NoProfile -NonInteractive -File (Join-Path $PSScriptRoot 'test-native-component-manifest.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Native component inventory validation failed.' }
Get-Command dotnet -ErrorAction Stop | Out-Null
$sourceCommit = & git -C $repository rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Source revision could not be read.' }
$sourceStatus = @(& git -C $repository status --porcelain)
if ($LASTEXITCODE -ne 0) { throw 'Source working-tree status could not be read.' }

$environmentNames = @('DOTNET_CLI_HOME','DOTNET_GENERATE_ASPNET_CERTIFICATE','DOTNET_CLI_TELEMETRY_OPTOUT','MSBUILDDISABLENODEREUSE','DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER')
$previousEnvironment = @{}
foreach ($name in $environmentNames) { $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
$report = [ordered]@{
    schemaVersion = 1
    kind = 'unqualified-native-host-candidate'
    releaseReady = $false
    sourceCommit = $sourceCommit.Trim()
    sourceWorkingTreeDirty = ($sourceStatus.Count -ne 0)
    runtimeIdentifier = 'win-x64'
    selfContained = $false
    installed = $false
    services = @()
}
try {
    $env:DOTNET_CLI_HOME = Join-Path $runtimeRoot 'dotnet-home'
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:MSBUILDDISABLENODEREUSE = '1'
    $env:DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER = '1'
    New-Item -ItemType Directory -Path $outputRoot -ErrorAction Stop | Out-Null
    foreach ($hostItem in $hosts) {
        $projectPath = Join-Path $repository $hostItem.project
        $outputPath = Join-Path $outputRoot $hostItem.id
        & dotnet publish $projectPath --configuration Release --runtime win-x64 --self-contained false --output $outputPath -m:1 /nodeReuse:false /p:UseAppHost=true /p:PublishSingleFile=false /p:IsTransformWebConfigDisabled=true
        if ($LASTEXITCODE -ne 0) { throw "Native host publish failed for $($hostItem.id)." }
        [xml] $project = Get-Content -LiteralPath $projectPath -Raw
        $assemblyName = [string]$project.Project.PropertyGroup.AssemblyName
        if ($assemblyName -notmatch '^FinancialAssistant\.[A-Za-z]+(\.Api)?$') { throw 'Unexpected native assembly name.' }
        $executable = Join-Path $outputPath ($assemblyName + '.exe')
        if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) { throw 'Native executable was not published.' }
        $report.services += [ordered]@{
            id = $hostItem.id
            serviceName = $hostItem.serviceName
            executable = "$($hostItem.id)/$assemblyName.exe"
            executableSha256 = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash.ToLowerInvariant()
            arguments = @('--environment','WindowsNative')
        }
    }
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $outputRoot 'candidate.json') -Encoding utf8NoBOM
    $report | ConvertTo-Json -Depth 8
}
finally {
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], 'Process') }
}
