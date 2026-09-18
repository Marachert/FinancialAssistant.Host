param([string]$ManifestPath = 'infra/windows-native/component-manifest.json')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not [IO.Path]::IsPathRooted($ManifestPath)) { $ManifestPath = Join-Path $root $ManifestPath }
$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json -AsHashtable
if ($manifest.schemaVersion -ne 1 -or $manifest.kind -ne 'component-design' -or
    $manifest.releaseReady -isnot [bool] -or $manifest.releaseReady) {
    throw 'Only the non-release component-design schema is supported.'
}
if ($manifest.sourceCommit -notmatch '^[a-f0-9]{40}$') { throw 'Invalid source commit.' }
foreach ($key in @('containersAllowed','automaticDownloadsAllowed','automaticSpendingAllowed','sharedDependencyRemovalAllowed','prereleaseAllowed')) {
    if ($manifest.policy[$key] -isnot [bool] -or $manifest.policy[$key]) { throw "Unsafe policy: $key" }
}
if ($manifest.policy.uninstallRetainsData -isnot [bool] -or -not $manifest.policy.uninstallRetainsData) { throw 'Data retention is required.' }
if ($manifest.policy.internalBindAddress -cne '127.0.0.1') { throw 'Internal binding must be loopback.' }
$targets = @('windows-11','windows-server-2022','windows-server-2025')
if ($manifest.targets.Count -ne 3 -or @(Compare-Object $targets @($manifest.targets.id)).Count -ne 0) { throw 'Target matrix mismatch.' }
foreach ($target in $manifest.targets) {
    if ($target.architecture -cne 'x64' -or $target.desktopExperience -isnot [bool] -or
        -not $target.desktopExperience -or $target.qualification -cne 'not-tested') { throw 'Invalid target qualification.' }
}
$components = @($manifest.packages) + @($manifest.hosts) + @($manifest.assets)
$ids = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($component in $components) {
    if ($component.id -notmatch '^[a-z][a-z0-9-]+$' -or -not $ids.Add($component.id)) { throw 'Invalid or duplicate component id.' }
    if ($component.owner -notmatch '^FIN-\d+$') { throw "Missing owner: $($component.id)" }
}
foreach ($required in @('admin-web','wpf-wizard','installation-engine')) {
    if ($required -cnotin @($manifest.assets | ForEach-Object { $_.id })) { throw "Missing required asset: $required" }
}
foreach ($asset in $manifest.assets) {
    if ($asset['qualification'] -isnot [string] -or $asset['qualification'] -cnotin @('not-implemented','not-tested','blocked')) {
        throw "Invalid asset qualification: $($asset.id)"
    }
}
foreach ($component in $components) {
    foreach ($dependency in $component.dependsOn) {
        if (-not $ids.Contains($dependency) -or $dependency -eq $component.id) { throw "Invalid dependency: $($component.id)" }
    }
}
# Validate the dependency graph without executing any package or probe.
$remaining = [Collections.Generic.HashSet[string]]::new($ids, [StringComparer]::OrdinalIgnoreCase)
while ($remaining.Count -gt 0) {
    $ready = @($components | Where-Object {
        $remaining.Contains($_.id) -and @($_.dependsOn | Where-Object { $remaining.Contains($_) }).Count -eq 0
    })
    if ($ready.Count -eq 0) { throw 'Dependency cycle detected.' }
    foreach ($component in $ready) { $null = $remaining.Remove($component.id) }
}
$ports = [Collections.Generic.HashSet[int]]::new()
$projects = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($hostComponent in $manifest.hosts) {
    if ($hostComponent.bindAddress -cne '127.0.0.1' -or $hostComponent.qualification -cne 'not-tested') { throw 'Invalid host boundary.' }
    if ($hostComponent.port -isnot [long] -and $hostComponent.port -isnot [int]) { throw 'Invalid port type.' }
    if ($hostComponent.port -lt 1024 -or $hostComponent.port -gt 65535 -or -not $ports.Add($hostComponent.port)) { throw 'Invalid or duplicate host port.' }
    $relative = $hostComponent.project
    if ([IO.Path]::IsPathRooted($relative) -or '..' -in $relative.Split([char[]]@('/','\'))) { throw 'Project path escapes repository.' }
    $path = [IO.Path]::GetFullPath((Join-Path $root $relative))
    if (-not $path.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath $path -PathType Leaf) -or -not $projects.Add($relative.Replace('\','/'))) { throw 'Missing or duplicate project.' }
    if ($hostComponent.liveness -cne '/health/live' -or $hostComponent.readiness -cne '/health/ready') { throw 'Health contract mismatch.' }
}
$expected = @(foreach ($folder in @('backend/services','backend/gateways')) {
    Get-ChildItem -LiteralPath (Join-Path $root $folder) -Filter '*.csproj' -Recurse -File | ForEach-Object {
        $xml = [xml](Get-Content -LiteralPath $_.FullName -Raw)
        if ($xml.Project.Sdk -eq 'Microsoft.NET.Sdk.Web') { [IO.Path]::GetRelativePath($root, $_.FullName).Replace('\','/') }
    }
})
if (@(Compare-Object $expected @($projects)).Count -ne 0) { throw 'Manifest does not cover exactly the production web hosts.' }
foreach ($package in $manifest.packages) {
    if ($package.qualification -notin @('metadata-only','blocked') -or $package.architecture -cne 'x64') { throw 'Invalid package qualification.' }
    if ($package.version -notmatch '^\d+(\.\d+){1,3}$') { throw 'Unpinned package version.' }
    $uri = [uri]$package.origin
    if (-not $uri.IsAbsoluteUri -or $uri.Scheme -cne 'https' -or $uri.UserInfo) { throw 'Invalid package origin.' }
    if ($null -eq $package.hash) {
        if ($package.qualification -cne 'blocked' -or $null -ne $package.hashAlgorithm) { throw 'Missing package digest must block qualification.' }
    } else {
        $pattern = switch ($package.hashAlgorithm) { 'SHA256' { '^[a-f0-9]{64}$' } 'SHA512' { '^[a-f0-9]{128}$' } default { throw 'Unsupported digest.' } }
        if ($package.hash -cnotmatch $pattern -or [string]::IsNullOrWhiteSpace($package.integritySource)) { throw 'Invalid package digest evidence.' }
    }
    foreach ($port in $package.ports) {
        if (($port -isnot [long] -and $port -isnot [int]) -or $port -lt 1024 -or $port -gt 65535 -or -not $ports.Add($port)) { throw 'Invalid or conflicting dependency port.' }
    }
}
function Assert-RequiredEntries([object[]]$Entries, [string]$Key, [string[]]$Required, [string]$Kind) {
    $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($entry in $Entries) {
        if ([string]::IsNullOrWhiteSpace($entry[$Key]) -or -not $names.Add($entry[$Key])) { throw "Invalid or duplicate $Kind identifier." }
        if ($entry.owner -notmatch '^FIN-\d+$') { throw "Missing $Kind owner." }
        if ($Kind -eq 'capability') {
            if ($entry.qualification -notin @('not-implemented','not-tested','blocked') -or [string]::IsNullOrWhiteSpace($entry.decision)) { throw 'Invalid capability qualification.' }
        } elseif ([string]::IsNullOrWhiteSpace($entry.detail)) { throw 'Missing blocker detail.' }
    }
    foreach ($name in $Required) {
        if (-not $names.Contains($name)) { throw "Missing required ${Kind}: $name" }
    }
}
Assert-RequiredEntries $manifest.capabilities 'id' @('receipt-storage','cache','event-delivery','search','metrics-traces-alerts','https','secret-recovery','backup','providers') 'capability'
Assert-RequiredEntries $manifest.blockers 'code' @('PACKAGE-QUALIFICATION','SEARCH-OBSERVABILITY','SIGNED-ARTIFACTS','RUNTIME-IMPLEMENTATION','HOST-ACCEPTANCE') 'blocker'
[pscustomobject]@{
    schemaValid = $true
    releaseReady = $false
    hosts = $manifest.hosts.Count
    packages = $manifest.packages.Count
    assets = $manifest.assets.Count
    targets = $manifest.targets.Count
    blockers = @($manifest.blockers.code)
    message = 'Inventory validated only. No packages downloaded, installed, executed or approved.'
} | ConvertTo-Json -Depth 5
