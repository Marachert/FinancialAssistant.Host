param(
    [Parameter(Mandatory)][string]$PackageId,
    [Parameter(Mandatory)][string]$PayloadPath,
    [string]$ManifestPath = 'infra/windows-native/component-manifest.json',
    [ValidateRange(1,1000000)][int]$MaxArchiveEntries = 100000,
    [ValidateRange(1,1099511627776)][long]$MaxExpandedBytes = 12884901888
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not [IO.Path]::IsPathRooted($ManifestPath)) { $ManifestPath = Join-Path $root $ManifestPath }
$null = & (Join-Path $PSScriptRoot 'test-native-component-manifest.ps1') -ManifestPath $ManifestPath
$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json -AsHashtable
$matches = @($manifest.packages | Where-Object { $_.id -ceq $PackageId })
if ($matches.Count -ne 1) { throw 'Package ID must identify one manifest entry.' }
$package = $matches[0]
if ($null -eq $package.hash) { throw 'Publisher digest is required before payload inspection.' }
$payload = Get-Item -LiteralPath $PayloadPath
if ($payload.PSIsContainer) { throw 'Payload must be a file.' }
$extension = [IO.Path]::GetExtension(([uri]$package.origin).AbsolutePath).ToLowerInvariant()
if ($extension -notin @('.zip','.exe','.msi')) { throw 'Unsupported payload format.' }

$stream = $null
$archive = $null
try {
    # Hash and inspect one read-locked stream so a file cannot be replaced between checks.
    $stream = [IO.File]::Open($payload.FullName,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
    $digest = (Get-FileHash -InputStream $stream -Algorithm $package.hashAlgorithm).Hash.ToLowerInvariant()
    if ($digest -cne $package.hash) { throw 'Payload digest does not match publisher evidence.' }
    $stream.Position = 0
    $entryCount = 0
    $fileCount = 0
    $expandedBytes = [long]0
    $noticeCandidates = [Collections.Generic.List[string]]::new()
    $noticeCount = 0
    if ($extension -eq '.zip') {
        try { $archive = [IO.Compression.ZipArchive]::new($stream,[IO.Compression.ZipArchiveMode]::Read,$true) }
        catch { throw 'Invalid ZIP archive.' }
        if ($archive.Entries.Count -gt $MaxArchiveEntries) { throw 'Archive entry limit exceeded.' }
        $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        $fileNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($entry in $archive.Entries) {
            $entryCount++
            $name = $entry.FullName.Replace('\','/')
            $segments = $name.TrimEnd('/').Split('/')
            if ([string]::IsNullOrWhiteSpace($name) -or $name.StartsWith('/') -or
                $name -match '[:\x00-\x1F<>"|?*]' -or @($segments | Where-Object {
                    $_ -in @('','.', '..') -or $_.EndsWith('.') -or $_.EndsWith(' ') -or
                    $_ -match '^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])([.]|$)'
                }).Count -gt 0) {
                throw 'Unsafe archive path.'
            }
            if (-not $names.Add($name.TrimEnd('/'))) { throw 'Duplicate Windows archive path.' }
            $unixType = ($entry.ExternalAttributes -shr 16) -band 0xF000
            if ($unixType -eq 0xA000) { throw 'Archive symbolic links are not supported.' }
            if ($name.EndsWith('/')) { continue }
            $null = $fileNames.Add($name)
            $fileCount++
            if ($entry.Length -gt $MaxExpandedBytes - $expandedBytes) { throw 'Archive expanded-byte limit exceeded.' }
            $expandedBytes += $entry.Length
            if ([IO.Path]::GetFileName($name) -match '(^|[_-])(LICENSE|LICENCE|NOTICE|COPYRIGHT)([._-]|$)') {
                $noticeCount++
                if ($noticeCandidates.Count -lt 100) { $noticeCandidates.Add($name) }
            }
        }
        foreach ($name in $names) {
            $parent = $name
            while ($parent.Contains('/')) {
                $parent = $parent.Substring(0,$parent.LastIndexOf('/'))
                if ($fileNames.Contains($parent)) { throw 'Archive file is also a parent directory.' }
            }
        }
        if ($fileCount -eq 0) { throw 'Archive has no payload files.' }
    }
    [ordered]@{
        packageId = $package.id
        version = $package.version
        digestVerified = $true
        hashAlgorithm = $package.hashAlgorithm
        hash = $digest
        integritySource = $package.integritySource
        downloadBytes = $stream.Length
        format = $extension.TrimStart('.')
        archiveEntries = $entryCount
        archiveFiles = $fileCount
        declaredExpandedBytes = if ($extension -eq '.zip') { $expandedBytes } else { $null }
        noticeCandidates = @($noticeCandidates)
        noticeCandidateCount = $noticeCount
        noticeCandidatesTruncated = $noticeCount -gt $noticeCandidates.Count
        binarySignaturesVerified = $false
        licenseReviewComplete = $false
        releaseReady = $false
        message = 'Digest and ZIP metadata only. No download, extraction, execution, installation or release approval. ZIP data/CRC and binary trust are not verified.'
    } | ConvertTo-Json -Depth 5
} finally {
    if ($null -ne $archive) { $archive.Dispose() }
    if ($null -ne $stream) { $stream.Dispose() }
}
