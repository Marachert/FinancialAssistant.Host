param([Parameter(Mandatory)][string] $ArchiveCacheDirectory,
    [string] $SevenZipPath = 'C:\Program Files\7-Zip\7z.exe')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (!$IsWindows) { throw 'This isolated package preparation requires Windows.' }
$sevenZip = [IO.Path]::GetFullPath($SevenZipPath)
if (!(Test-Path -LiteralPath $sevenZip -PathType Leaf)) { throw 'Existing extraction tool is unavailable.' }
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$runtime = Join-Path $repo '.codex-runtime'
$archives = [IO.Path]::GetFullPath($ArchiveCacheDirectory)
if (-not (Test-Path -LiteralPath $archives -PathType Container)) { throw 'Existing local archive cache is required; this tool never downloads packages.' }
$manifest = Get-Content -LiteralPath (Join-Path $repo 'infra/windows-native/component-manifest.json') -Raw | ConvertFrom-Json
$cache = [IO.Path]::GetFullPath((Join-Path $runtime 'broker-pkgs'))
$workspace = $repo
if (!$cache.StartsWith($workspace + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe fixture root.' }
for ($ancestor = $cache; $ancestor; $ancestor = [IO.Path]::GetDirectoryName($ancestor)) {
    if ((Test-Path -LiteralPath $ancestor) -and ((Get-Item -LiteralPath $ancestor).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Reparse points are not fixture roots.' }
}
$run = Join-Path $cache ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $run -Force | Out-Null
$prepared = $false
$results = @()
$expandedTotal = 0L
try {
    foreach ($id in @('erlang','rabbitmq')) {
        $package = @($manifest.packages | Where-Object id -eq $id)
        if ($package.Count -ne 1 -or $package[0].hashAlgorithm -ne 'SHA256') { throw 'Pinned package metadata is incomplete.' }
        $fileName = [IO.Path]::GetFileName(([Uri]$package[0].origin).AbsolutePath)
        $archive = Join-Path $archives $fileName
        $destination = Join-Path $run $(if ($id -eq 'erlang') { 'e' } else { 'r' })
        $sourceLock = [IO.File]::Open($archive, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
        try {
            $digest = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($digest -ne $package[0].hash.ToLowerInvariant()) { throw 'Pinned package digest mismatch.' }
            if ((Get-AuthenticodeSignature -LiteralPath $archive).Status -ne 'Valid') { throw 'Package signature did not validate.' }
            $listing = @(& $sevenZip l -slt -ba -- $archive 2>&1)
            if ($LASTEXITCODE -ne 0) { throw 'Package listing failed.' }
            $paths = @($listing | Where-Object { $_ -is [string] -and $_.StartsWith('Path = ') } | ForEach-Object { $_.Substring(7) })
            if ($paths.Count -lt 1 -or $paths.Count -gt 50000) { throw 'Invalid package entry inventory.' }
            foreach ($entry in $paths) {
                $segments = $entry -split '[\\/]'
                if ([string]::IsNullOrWhiteSpace($entry) -or $entry -match '^[\\/]|:' -or $entry.ToCharArray().Where({ [char]::IsControl($_) }).Count -gt 0 -or
                    @($segments | Where-Object { $_ -in @('','..','.') }).Count -gt 0) { throw 'Unsafe package entry path.' }
                $target = [IO.Path]::GetFullPath((Join-Path $destination $entry))
                if (!$target.StartsWith($destination + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Package entry escapes extraction root.' }
            }
            if (@($listing | Where-Object { $_ -match '^(Symbolic Link|Hard Link|Alternate Stream) = .+' }).Count) { throw 'Archive links or streams are not permitted.' }
            # Reserve two GiB for this entire bundle and two GiB of remaining headroom.
            $remainingBudget = 4GB - $expandedTotal
            if (([IO.DriveInfo]::new([IO.Path]::GetPathRoot($destination))).AvailableFreeSpace -lt $remainingBudget) { throw 'Insufficient conservative fixture extraction budget.' }
            New-Item -ItemType Directory -Path $destination | Out-Null
            $output = @(& $sevenZip x -y "-o$destination" -- $archive 2>&1)
            if ($LASTEXITCODE -ne 0) { throw 'Isolated package extraction failed.' }
            $entries = @(Get-ChildItem -LiteralPath $destination -Recurse -Force)
            if (@($entries | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count) { throw 'Extracted reparse point detected.' }
            $files = @($entries | Where-Object { !$_.PSIsContainer })
            $expandedBytes = ($files | Measure-Object Length -Sum).Sum
            $expandedTotal += $expandedBytes
            if ($expandedTotal -gt 2GB) { throw 'Fixture extraction exceeded its bounded development budget.' }
            $results += @{id=$id;version=$package[0].version;archiveDigest=$digest;signatureValid=$true;validatedEntries=$paths.Count;files=$files.Count;expandedBytes=$expandedBytes;root=[IO.Path]::GetRelativePath($runtime,$destination)}
        } finally { $sourceLock.Dispose() }
    }
    $prepared = $true
    Write-Output ('Isolated package extraction completed: ' + [IO.Path]::GetFileName($run))
} finally {
    @{prepared=$prepared;vendorPayloadExecuted=$false;installed=$false;releaseReady=$false;expandedBytes=$expandedTotal;results=$results;verifiedAt=[DateTime]::UtcNow.ToString('o')} |
        ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $run 'preparation.json') -Encoding utf8NoBOM
}
