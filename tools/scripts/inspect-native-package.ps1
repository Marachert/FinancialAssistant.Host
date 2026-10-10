param(
    [Parameter(Mandatory)][string]$PackageId,
    [Parameter(Mandatory)][string]$PayloadPath,
    [string]$ManifestPath = 'infra/windows-native/component-manifest.json',
    [ValidateRange(1,1000000)][int]$MaxArchiveEntries = 100000,
    [ValidateRange(1,1073741824)][long]$MaxCentralDirectoryBytes = 67108864,
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

function Read-ZipBytes([IO.Stream]$Source,[long]$Offset,[int]$Count) {
    if ($Offset -lt 0 -or $Offset -gt $Source.Length - $Count) { throw 'Invalid ZIP archive.' }
    $Source.Position = $Offset
    $buffer = [byte[]]::new($Count)
    try { $Source.ReadExactly($buffer,0,$Count) } catch { throw 'Invalid ZIP archive.' }
    return ,$buffer
}
function Test-ZipDirectoryBounds([IO.Stream]$Source) {
    if (-not [BitConverter]::IsLittleEndian -or $Source.Length -lt 22) { throw 'Invalid ZIP archive.' }
    # Locate EOCD within its bounded comment tail, before ZipArchive loads entry objects.
    $tailSize = [int][Math]::Min($Source.Length,65557)
    $tailOffset = $Source.Length - $tailSize
    $tail = Read-ZipBytes $Source $tailOffset $tailSize
    $endIndex = -1
    for ($index = $tail.Length - 22; $index -ge 0; $index--) {
        if ([BitConverter]::ToUInt32($tail,$index) -eq 0x06054B50 -and
            $index + 22 + [BitConverter]::ToUInt16($tail,$index+20) -eq $tail.Length) { $endIndex = $index; break }
    }
    if ($endIndex -lt 0) { throw 'Invalid ZIP archive.' }
    if ([BitConverter]::ToUInt16($tail,$endIndex+4) -ne 0 -or [BitConverter]::ToUInt16($tail,$endIndex+6) -ne 0) { throw 'Multi-disk ZIP archives are not supported.' }
    $diskCount = [uint64][BitConverter]::ToUInt16($tail,$endIndex+8)
    $expectedCount = [uint64][BitConverter]::ToUInt16($tail,$endIndex+10)
    $directorySize = [uint64][BitConverter]::ToUInt32($tail,$endIndex+12)
    $directoryOffset = [uint64][BitConverter]::ToUInt32($tail,$endIndex+16)
    $metadataStart = $tailOffset + $endIndex
    if ($expectedCount -eq [uint16]::MaxValue -or $diskCount -eq [uint16]::MaxValue -or
        $directorySize -eq [uint32]::MaxValue -or $directoryOffset -eq [uint32]::MaxValue) {
        $locatorOffset = $metadataStart - 20
        $locator = Read-ZipBytes $Source $locatorOffset 20
        if ([BitConverter]::ToUInt32($locator,0) -ne 0x07064B50 -or
            [BitConverter]::ToUInt32($locator,4) -ne 0 -or [BitConverter]::ToUInt32($locator,16) -ne 1) { throw 'Invalid ZIP64 locator.' }
        $zip64Offset = [BitConverter]::ToUInt64($locator,8)
        if ($locatorOffset -lt 56 -or $zip64Offset -gt [uint64]($locatorOffset - 56)) { throw 'Invalid ZIP64 offset.' }
        $zip64 = Read-ZipBytes $Source ([long]$zip64Offset) 56
        $recordSize = [BitConverter]::ToUInt64($zip64,4)
        if ([BitConverter]::ToUInt32($zip64,0) -ne 0x06064B50 -or $recordSize -lt 44 -or
            $recordSize -ne [uint64]($locatorOffset - [long]$zip64Offset - 12)) { throw 'Invalid ZIP64 record.' }
        if ([BitConverter]::ToUInt32($zip64,16) -ne 0 -or [BitConverter]::ToUInt32($zip64,20) -ne 0) { throw 'Multi-disk ZIP archives are not supported.' }
        $diskCount = [BitConverter]::ToUInt64($zip64,24)
        $expectedCount = [BitConverter]::ToUInt64($zip64,32)
        $directorySize = [BitConverter]::ToUInt64($zip64,40)
        $directoryOffset = [BitConverter]::ToUInt64($zip64,48)
        $metadataStart = [long]$zip64Offset
    }
    if ($expectedCount -gt [uint64]$MaxArchiveEntries) { throw 'Archive entry limit exceeded.' }
    if ($diskCount -ne $expectedCount) { throw 'Multi-disk ZIP archives are not supported.' }
    if ($directorySize -gt [uint64]$MaxCentralDirectoryBytes) { throw 'Archive central-directory byte limit exceeded.' }
    if ($directoryOffset -gt [uint64]$metadataStart -or $directorySize -gt [uint64]($metadataStart - [long]$directoryOffset)) { throw 'Invalid ZIP directory bounds.' }
    $directoryEnd = [long]$directoryOffset + [long]$directorySize
    $position = [long]$directoryOffset
    $actualCount = 0
    # Scan fixed record headers and skip variable fields; reject lying counts before materialization.
    while ($position -lt $directoryEnd) {
        if ($directoryEnd - $position -lt 46) { throw 'Invalid ZIP central directory.' }
        $header = Read-ZipBytes $Source $position 46
        if ([BitConverter]::ToUInt32($header,0) -ne 0x02014B50) { throw 'Invalid ZIP central directory.' }
        $actualCount++
        if ($actualCount -gt $MaxArchiveEntries) { throw 'Archive entry limit exceeded.' }
        $recordLength = 46 + [int][BitConverter]::ToUInt16($header,28) +
            [int][BitConverter]::ToUInt16($header,30) + [int][BitConverter]::ToUInt16($header,32)
        if ($recordLength -gt $directoryEnd - $position) { throw 'Invalid ZIP central directory.' }
        $position += $recordLength
    }
    if ([uint64]$actualCount -ne $expectedCount) { throw 'ZIP directory entry count mismatch.' }
    $Source.Position = 0
}

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
        Test-ZipDirectoryBounds $stream
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
                    $_.Length -gt 255 -or $_ -in @('','.', '..') -or $_.EndsWith('.') -or $_.EndsWith(' ') -or
                    $_ -match '^(CON|PRN|AUX|NUL|COM[1-9\u00B9\u00B2\u00B3]|LPT[1-9\u00B9\u00B2\u00B3])([.]|$)'
                }).Count -gt 0) {
                throw 'Unsafe archive path.'
            }
            if (-not $names.Add($name.TrimEnd('/'))) { throw 'Duplicate Windows archive path.' }
            $unixType = ($entry.ExternalAttributes -shr 16) -band 0xF000
            if ($unixType -eq 0xA000) { throw 'Archive symbolic links are not supported.' }
            if ($name.EndsWith('/')) {
                if ($entry.Length -ne 0) { throw 'Archive directory contains data.' }
                continue
            }
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
