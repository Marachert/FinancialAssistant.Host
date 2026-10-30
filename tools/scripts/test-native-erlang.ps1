param([Parameter(Mandatory)][string] $PreparedPackageDirectory)
$ErrorActionPreference = 'Stop'
if (-not $IsWindows -or $PSVersionTable.PSVersion.Major -lt 7) { throw 'PowerShell 7 on Windows is required.' }
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$runtime = Join-Path $repo '.codex-runtime'
$package = [IO.Path]::GetFullPath($PreparedPackageDirectory)
$packageItem = Get-Item -LiteralPath $package -ErrorAction Stop
$nonce = [guid]::Empty
if (-not $packageItem.PSIsContainer -or -not [guid]::TryParseExact($packageItem.Name, 'N', [ref]$nonce) -or
    $packageItem.Parent.Name -ne 'broker-pkgs' -or $packageItem.Parent.Parent.Name -ne '.codex-runtime') { throw 'An owned nonce package extraction is required.' }
$erlang = Join-Path $package 'e'
$erts = Join-Path $erlang 'erts-15.2.7.13/bin'
$bin = Join-Path $erlang 'bin'
$release = Join-Path $erlang 'releases/27'
$proof = Join-Path $runtime ('broker-proof/' + [guid]::NewGuid().ToString('N'))
$process = $null
$startedAt = $null
$result = [ordered]@{ passed = $false; installed = $false; releaseReady = $false; cleanupSucceeded = $false }
function Assert-OwnedPath([string] $Path) {
    $full = [IO.Path]::GetFullPath($Path)
    $prefix = [IO.Path]::GetFullPath($runtime).TrimEnd('\') + '\'
    $packagePrefix = $package.TrimEnd('\') + '\'
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) -and
        $full -ne $package -and -not $full.StartsWith($packagePrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Path escaped owned fixture/extraction.' }
    for ($cursor = $full; $cursor; $cursor = [IO.Path]::GetDirectoryName($cursor)) {
        if (Test-Path -LiteralPath $cursor) {
            if ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked runtime paths are forbidden.' }
        }
    }
    return $full
}
function Assert-NoLinks([string] $Path) {
    $null = Assert-OwnedPath $Path
    if (Get-ChildItem -LiteralPath $Path -Recurse -Force | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint } | Select-Object -First 1) {
        throw 'Linked package entries are forbidden.'
    }
}
try {
    $null = Assert-OwnedPath $proof
    $prepared = Get-Content -LiteralPath (Join-Path $package 'preparation.json') -Raw | ConvertFrom-Json
    $otp = @($prepared.results | Where-Object id -eq 'erlang')
    if ($prepared.prepared -ne $true -or $prepared.vendorPayloadExecuted -ne $false -or $prepared.installed -ne $false -or
        $otp.Count -ne 1 -or $otp[0].archiveDigest -ne '7ed735569fb9bc44368e40d86a08c71f1b70cba5a18154417b882214e1da66d4' -or
        $otp[0].signatureValid -ne $true -or $otp[0].version -ne '27.3.4.17') { throw 'Pinned Erlang preparation is not verified.' }
    Assert-NoLinks $erlang
    $bootstrap = Join-Path $package 'isolated-erlang-bootstrap.json'
    if (Test-Path -LiteralPath $bin) {
        if (-not (Test-Path -LiteralPath $bootstrap)) { throw 'Unexpected existing Erlang bin directory.' }
        $previous = Get-Content -LiteralPath $bootstrap -Raw | ConvertFrom-Json
        if ($previous.packageDigest -ne $otp[0].archiveDigest -or $previous.installed -ne $false) { throw 'Invalid owned bootstrap marker.' }
        foreach ($file in $previous.generatedFiles) {
            $target = Assert-OwnedPath (Join-Path $erlang $file.path)
            if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $file.sha256) { throw 'Owned Erlang bootstrap was modified.' }
        }
    } else {
        if (@(Get-ChildItem -LiteralPath $erlang -Recurse -File -Force).Count -ne 5224) { throw 'Unexpected extracted Erlang file count.' }
        $null = New-Item -ItemType Directory -Path $bin
        $generated = [Collections.Generic.List[string]]::new()
        # Reproduce only workspace launcher/boot files documented by OTP's Install.c;
        # never execute Install.exe, create symlinks, or touch registry/service state.
        foreach ($name in @('erl.exe','erlc.exe','erl_call.exe','dialyzer.exe','typer.exe','escript.exe','ct_run.exe')) {
            $source = Assert-OwnedPath (Join-Path $erts $name)
            $target = Assert-OwnedPath (Join-Path $bin $name)
            Copy-Item -LiteralPath $source -Destination $target -ErrorAction Stop
            $generated.Add($target)
        }
        foreach ($name in @('start_clean.boot','start_sasl.boot','no_dot_erlang.boot')) {
            $target = Assert-OwnedPath (Join-Path $bin $name)
            Copy-Item -LiteralPath (Join-Path $release $name) -Destination $target -ErrorAction Stop
            $generated.Add($target)
        }
        $start = Join-Path $bin 'start.boot'
        Copy-Item -LiteralPath (Join-Path $release 'start_clean.boot') -Destination $start
        $generated.Add($start)
        $ini = "[erlang]`nBindir=$($erts.Replace('\','\\'))`nProgname=erl`nRootdir=$($erlang.Replace('\','\\'))`n"
        foreach ($directory in @($erts,$bin)) {
            $target = Assert-OwnedPath (Join-Path $directory 'erl.ini')
            if (Test-Path -LiteralPath $target) { throw 'Refusing to overwrite an existing Erlang ini.' }
            [IO.File]::WriteAllText($target, $ini, [Text.UTF8Encoding]::new($false))
            $generated.Add($target)
        }
        $files = @($generated | ForEach-Object {
            @{ path = [IO.Path]::GetRelativePath($erlang, $_); sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant() }
        })
        @{ packageDigest = $otp[0].archiveDigest; installed = $false; generatedFiles = $files;
            source = 'https://github.com/erlang/otp/blob/OTP-27.3.4.17/erts/etc/win32/Install.c' } |
            ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $bootstrap -Encoding utf8NoBOM
    }
    $null = New-Item -ItemType Directory -Path $proof
    foreach ($name in @('home','temp','appdata','localappdata')) { $null = New-Item -ItemType Directory -Path (Join-Path $proof $name) }
    $info = [Diagnostics.ProcessStartInfo]::new((Join-Path $bin 'erl.exe'))
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.WorkingDirectory = $proof
    foreach ($name in @($info.Environment.Keys)) {
        if ($name -match '^(ERL_|RABBITMQ_)|TOKEN|SECRET|KEY|PASSWORD|CREDENTIAL|CONNECTIONSTRING|ATLASSIAN|GITHUB|OPENAI|AZURE|AWS|GOOGLE') { $null = $info.Environment.Remove($name) }
    }
    $info.Environment['HOME'] = Join-Path $proof 'home'
    $info.Environment['USERPROFILE'] = Join-Path $proof 'home'
    $info.Environment['HOMEDRIVE'] = [IO.Path]::GetPathRoot($proof).TrimEnd('\')
    $info.Environment['HOMEPATH'] = (Join-Path $proof 'home').Substring(2)
    $info.Environment['APPDATA'] = Join-Path $proof 'appdata'
    $info.Environment['LOCALAPPDATA'] = Join-Path $proof 'localappdata'
    $info.Environment['TEMP'] = Join-Path $proof 'temp'
    $info.Environment['TMP'] = Join-Path $proof 'temp'
    $info.Environment['ERL_CRASH_DUMP'] = Join-Path $proof 'erl-crash.dump'
    foreach ($argument in @('+S','2:2','-noshell','-noinput','-start_epmd','false','-boot',(Join-Path $bin 'no_dot_erlang'),
        '-eval','io:format("FIN276_OTP=~s_ERTS=~s~n",[erlang:system_info(otp_release),erlang:system_info(version)]),halt(0).')) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($info)
    $startedAt = $process.StartTime.ToUniversalTime()
    $output = $process.StandardOutput.ReadToEndAsync()
    $errors = $process.StandardError.ReadToEndAsync()
    $null = $process.WaitForExitAsync().WaitAsync([TimeSpan]::FromSeconds(30)).GetAwaiter().GetResult()
    $stdout = $output.GetAwaiter().GetResult()
    $stderr = $errors.GetAwaiter().GetResult()
    [IO.File]::WriteAllText((Join-Path $proof 'stdout.log'), $stdout)
    [IO.File]::WriteAllText((Join-Path $proof 'stderr.log'), $stderr)
    $result.exitCode = $process.ExitCode
    $result.passed = $process.ExitCode -eq 0 -and $stdout.Trim() -eq 'FIN276_OTP=27_ERTS=15.2.7.13' -and $stderr.Length -eq 0
    if (-not $result.passed) { throw 'Isolated Erlang probe failed; inspect owned logs.' }
} finally {
    if ($process) {
        if (-not $process.HasExited) {
            if ($process.StartTime.ToUniversalTime() -ne $startedAt -or
                $process.MainModule.FileName -ne (Join-Path $bin 'erl.exe')) { throw 'Refusing to stop an unverified process.' }
            $process.Kill($true)
            if (-not $process.WaitForExit(10000)) { throw 'Owned Erlang process did not exit.' }
        }
        $result.cleanupSucceeded = $process.HasExited
        $process.Dispose()
    }
    $result.verifiedAt = [DateTimeOffset]::UtcNow.ToString('O')
    if (Test-Path -LiteralPath $proof) { $result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $proof 'result.json') -Encoding utf8NoBOM }
    $result | ConvertTo-Json -Depth 6
}
