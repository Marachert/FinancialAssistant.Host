param([Parameter(Mandatory)][string] $PreparedPackageDirectory,
    [ValidateSet('Transport','Financial')][string] $Scenario = 'Transport',
    [string] $PostgresArchivePath)
$ErrorActionPreference = 'Stop'
if (-not $IsWindows -or $PSVersionTable.PSVersion.Major -lt 7) { throw 'PowerShell 7 on Windows is required.' }
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$runtime = Join-Path $repo '.codex-runtime'
$package = [IO.Path]::GetFullPath($PreparedPackageDirectory)
if ($Scenario -eq 'Financial' -and (-not $PostgresArchivePath -or -not (Test-Path -LiteralPath $PostgresArchivePath -PathType Leaf))) { throw 'Financial lifecycle requires the pinned PostgreSQL archive.' }
if ($PostgresArchivePath) { $PostgresArchivePath = [IO.Path]::GetFullPath($PostgresArchivePath) }
function Assert-ReadPath([string] $Path) {
    $item = Get-Item -LiteralPath ([IO.Path]::GetFullPath($Path)) -Force -ErrorAction Stop
    for ($cursor = $item; $cursor; $cursor = if ($cursor.PSIsContainer) { $cursor.Parent } else { $cursor.Directory }) {
        if ($cursor.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked package or build source is forbidden.' }
    }
    return $item.FullName
}
$null = Assert-ReadPath $package
$erlang = Join-Path $package 'e'
$rabbit = Join-Path $package 'r/rabbitmq_server-4.3.5'
$plugins = Join-Path $package 'r/$_52_'
$proof = Join-Path $runtime ('broker-proof/' + [guid]::NewGuid().ToString('N'))
$owned = [Collections.Generic.List[object]]::new()
$reservations = [Collections.Generic.List[object]]::new()
$result = [ordered]@{ passed = $false; proofId = [IO.Path]::GetFileName($proof); syntheticOnly = $true; installed = $false; releaseReady = $false; cleanupSucceeded = $false; stage = 'preflight' }
function Assert-OwnedPath([string] $Path) {
    $full = [IO.Path]::GetFullPath($Path)
    $prefix = [IO.Path]::GetFullPath($runtime).TrimEnd('\') + '\'
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Path escaped owned runtime root.' }
    for ($cursor = $full; $cursor -and $cursor.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase); $cursor = [IO.Path]::GetDirectoryName($cursor)) {
        if (Test-Path -LiteralPath $cursor) {
            if ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked runtime paths are forbidden.' }
        }
    }
    return $full
}
function Start-Owned([string] $Exe, [string[]] $Arguments, [hashtable] $Extra, [string] $Name) {
    $info = [Diagnostics.ProcessStartInfo]::new($Exe)
    $info.UseShellExecute = $false; $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true; $info.WorkingDirectory = $proof
    foreach ($key in @($info.Environment.Keys)) {
        if ($key -match '^(ERL_|RABBITMQ_)|TOKEN|SECRET|KEY|PASSWORD|CREDENTIAL|CONNECTIONSTRING|ATLASSIAN|GITHUB|OPENAI|AZURE|AWS|GOOGLE') { $null = $info.Environment.Remove($key) }
    }
    $info.Environment['HOME'] = Join-Path $proof 'home'; $info.Environment['USERPROFILE'] = Join-Path $proof 'home'
    $info.Environment['HOMEDRIVE'] = [IO.Path]::GetPathRoot($proof).TrimEnd('\')
    $info.Environment['HOMEPATH'] = (Join-Path $proof 'home').Substring(2)
    $info.Environment['APPDATA'] = Join-Path $proof 'appdata'; $info.Environment['LOCALAPPDATA'] = Join-Path $proof 'localappdata'
    $info.Environment['TEMP'] = Join-Path $proof 'temp'; $info.Environment['TMP'] = Join-Path $proof 'temp'
    $info.Environment['DOTNET_CLI_HOME'] = Join-Path $proof 'home'; $info.Environment['DOTNET_CLI_TELEMETRY_OPTOUT'] = '1'
    $info.Environment['DOTNET_GENERATE_ASPNET_CERTIFICATE'] = 'false'
    foreach ($key in $Extra.Keys) { $info.Environment[$key] = [string]$Extra[$key] }
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($info)
    $entry = [pscustomobject]@{ Process = $process; Exe = [IO.Path]::GetFullPath($Exe); StartedAt = $process.StartTime.ToUniversalTime();
        Output = $process.StandardOutput.ReadToEndAsync(); Errors = $process.StandardError.ReadToEndAsync(); Name = $Name; Captured = $false }
    $owned.Add($entry)
    return $entry
}
function Capture-Owned($Entry) {
    if ($Entry.Captured) { return }
    [IO.File]::WriteAllText((Join-Path $proof ($Entry.Name + '.stdout.log')), $Entry.Output.GetAwaiter().GetResult())
    [IO.File]::WriteAllText((Join-Path $proof ($Entry.Name + '.stderr.log')), $Entry.Errors.GetAwaiter().GetResult())
    $Entry.Captured = $true
}
function Stop-Owned($Entry) {
    $process = $Entry.Process
    if (-not $process.HasExited) {
        if ($process.StartTime.ToUniversalTime() -ne $Entry.StartedAt -or $process.MainModule.FileName -ne $Entry.Exe) { throw 'Refusing to stop an unverified process.' }
        $process.Kill($true)
        if (-not $process.WaitForExit(15000)) { throw 'Owned process did not exit.' }
    }
    Capture-Owned $Entry
}
function Run-Fixture([string] $Mode, [int] $Seconds = 60, [switch] $StartOnly) {
    $assembly = if ($Scenario -eq 'Financial') { 'FinancialAssistant.NativeLifecycle.Integration' } else { 'FinancialAssistant.Shared.Messaging.Integration' }
    $arguments = @((Join-Path $proof ("client/$assembly.dll")), $Mode, $proof)
    if ($Mode -eq 'prepare') { $arguments += [string]$amqpPort }
    $entry = Start-Owned $dotnet $arguments @{ FA_NATIVE_BROKER_SYNTHETIC_ONLY = '1' } ('fixture-' + $Mode)
    if ($StartOnly) { return $entry }
    $null = $entry.Process.WaitForExitAsync().WaitAsync([TimeSpan]::FromSeconds($Seconds)).GetAwaiter().GetResult()
    Capture-Owned $entry
    if ($entry.Process.ExitCode -ne 0) { throw "Synthetic broker fixture failed in $Mode; inspect owned logs." }
}
function Wait-Listener([int] $Port, $Entry, [int] $Seconds) {
    $until = [DateTimeOffset]::UtcNow.AddSeconds($Seconds)
    while ([DateTimeOffset]::UtcNow -lt $until) {
        if ($Entry.Process.HasExited) { Capture-Owned $Entry; throw 'Owned broker process exited before readiness.' }
        $client = [Net.Sockets.TcpClient]::new()
        try {
            $null = $client.ConnectAsync([Net.IPAddress]::Loopback, $Port).WaitAsync([TimeSpan]::FromSeconds(1)).GetAwaiter().GetResult()
            return
        } catch [Net.Sockets.SocketException] { } catch [TimeoutException] { }
        finally { $client.Dispose() }
        Start-Sleep -Milliseconds 250
    }
    throw 'Owned broker listener readiness timed out.'
}
try {
    $null = Assert-OwnedPath $proof
    $prepared = Get-Content -LiteralPath (Join-Path $package 'preparation.json') -Raw | ConvertFrom-Json
    if ($prepared.prepared -ne $true -or $prepared.installed -ne $false -or $prepared.vendorPayloadExecuted -ne $false) { throw 'Unverified package preparation.' }
    $expected = @{ erlang = '7ed735569fb9bc44368e40d86a08c71f1b70cba5a18154417b882214e1da66d4'; rabbitmq = 'e7f8a8dc326714ca5b964e27f987b020398a4a3f4b52c4d85d8f342b0b0e4e7d' }
    foreach ($id in $expected.Keys) {
        $match = @($prepared.results | Where-Object id -eq $id)
        if ($match.Count -ne 1 -or $match[0].signatureValid -ne $true -or $match[0].archiveDigest -ne $expected[$id]) { throw 'Unexpected pinned package digest.' }
    }
    foreach ($path in @($erlang,$rabbit,$plugins)) {
        $null = Assert-ReadPath $path
        if (Get-ChildItem -LiteralPath $path -Recurse -Force | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint } | Select-Object -First 1) { throw 'Linked package entries are forbidden.' }
    }
    if (-not (Test-Path -LiteralPath (Join-Path $plugins 'rabbit-4.3.5/ebin/rabbit.beam'))) { throw 'Pinned RabbitMQ application payload is missing.' }
    $bootstrap = Get-Content -LiteralPath (Join-Path $package 'isolated-erlang-bootstrap.json') -Raw | ConvertFrom-Json
    if ($bootstrap.packageDigest -ne $expected.erlang -or $bootstrap.installed -ne $false) { throw 'Unexpected isolated Erlang bootstrap.' }
    foreach ($file in $bootstrap.generatedFiles) {
        $path = Assert-ReadPath (Join-Path $erlang $file.path)
        if (-not $path.StartsWith($erlang.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Bootstrap entry escaped prepared package.' }
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $file.sha256) { throw 'Modified isolated Erlang bootstrap.' }
    }
    $drive = [IO.DriveInfo]::new([IO.Path]::GetPathRoot($proof))
    if ($drive.AvailableFreeSpace -lt 2GB) { throw 'Insufficient free space for bounded synthetic broker proof.' }
    $dotnet = (Get-Command dotnet -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
    $assembly = if ($Scenario -eq 'Financial') { 'FinancialAssistant.NativeLifecycle.Integration' } else { 'FinancialAssistant.Shared.Messaging.Integration' }
    $clientSource = Join-Path $repo "backend/shared/messaging/$assembly/bin/Release/net8.0"
    $null = Assert-ReadPath $clientSource
    if (-not (Test-Path -LiteralPath (Join-Path $clientSource "$assembly.dll"))) { throw 'Build the broker fixture first.' }
    $null = New-Item -ItemType Directory -Path $proof
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent().User
    $acl = [Security.AccessControl.DirectorySecurity]::new()
    $acl.SetAccessRuleProtection($true, $false); $acl.SetOwner($identity)
    $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($identity, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow'))
    Set-Acl -LiteralPath $proof -AclObject $acl
    foreach ($name in @('client','home','temp','appdata','appdata/erlang','localappdata','data','logs','expanded')) { $null = New-Item -ItemType Directory -Path (Join-Path $proof $name) }
    foreach ($source in Get-ChildItem -LiteralPath $clientSource -File) { Copy-Item -LiteralPath $source.FullName -Destination (Join-Path $proof ('client/' + $source.Name)) }
    foreach ($index in 1..3) { $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0); $listener.Start(); $reservations.Add($listener) }
    $amqpPort = $reservations[0].LocalEndpoint.Port; $epmdPort = $reservations[1].LocalEndpoint.Port; $distributionPort = $reservations[2].LocalEndpoint.Port
    $result.ports = @{ amqp = $amqpPort; epmd = $epmdPort; distribution = $distributionPort }
    Run-Fixture prepare
    $cookie = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    [IO.File]::WriteAllText((Join-Path $proof 'home/.erlang.cookie'), $cookie, [Text.Encoding]::ASCII)
    [IO.File]::WriteAllText((Join-Path $proof 'appdata/erlang/.erlang.cookie'), $cookie, [Text.Encoding]::ASCII)
    [IO.File]::WriteAllText((Join-Path $proof 'enabled_plugins'), '[].')
    $config = @(
        "listeners.tcp.1 = 127.0.0.1:$amqpPort",
        'loopback_users.guest = true',
        'definitions.import_backend = local_filesystem',
        ('definitions.local.path = ' + (Join-Path $proof 'definitions.json').Replace('\','/')),
        'disk_free_limit.absolute = 1GB', 'vm_memory_high_watermark.absolute = 512MB',
        'raft.wal_max_size_bytes = 32000000', 'max_message_size = 262144',
        ('log.file = ' + (Join-Path $proof 'logs/broker.log').Replace('\','/')), 'log.console = true'
    ) -join "`n"
    [IO.File]::WriteAllText((Join-Path $proof 'rabbitmq.conf'), $config, [Text.UTF8Encoding]::new($false))
    $environment = @{
        ERLANG_HOME = $erlang; ERL_LIBS = $plugins; ERL_EPMD_ADDRESS = '127.0.0.1'; ERL_EPMD_PORT = [string]$epmdPort;
        ERL_CRASH_DUMP = Join-Path $proof 'erl-crash.dump'; RABBITMQ_HOME = $rabbit; RABBITMQ_BASE = $proof;
        RABBITMQ_CONFIG_FILE = Join-Path $proof 'rabbitmq'; RABBITMQ_NODENAME = 'fa276_' + [IO.Path]::GetFileName($proof).Substring(0,12) + '@localhost';
        RABBITMQ_DIST_PORT = [string]$distributionPort; RABBITMQ_MNESIA_BASE = Join-Path $proof 'data';
        RABBITMQ_MNESIA_DIR = Join-Path $proof 'data/node'; RABBITMQ_LOG_BASE = Join-Path $proof 'logs';
        RABBITMQ_PLUGINS_EXPAND_DIR = Join-Path $proof 'expanded'; RABBITMQ_ENABLED_PLUGINS_FILE = Join-Path $proof 'enabled_plugins';
        RABBITMQ_PLUGINS_DIR = $plugins;
        RABBITMQ_PID_FILE = Join-Path $proof 'broker.pid'; RABBITMQ_FEATURE_FLAGS_FILE = Join-Path $proof 'data/feature-flags'
    }
    foreach ($listener in $reservations) { $listener.Stop() }
    $epmd = Start-Owned (Join-Path $erlang 'erts-15.2.7.13/bin/epmd.exe') @('-port', [string]$epmdPort, '-address', '127.0.0.1') $environment 'epmd'
    Wait-Listener $epmdPort $epmd 20
    $arguments = @('+S','2:2','-noinput','-noshell','-start_epmd','false',
        '-boot',(Join-Path $erlang 'bin/start_sasl'),'-kernel','inet_dist_use_interface','{127,0,0,1}',
        '-s','rabbit','boot','-syslog','logger','[]','-syslog','syslog_error_logger','false')
    $result.stage = 'initial-broker'
    $broker = Start-Owned (Join-Path $erlang 'bin/erl.exe') $arguments $environment 'broker-initial'
    Wait-Listener $amqpPort $broker 90
    if ($Scenario -eq 'Financial') {
        $result.stage = 'automatic-financial-postgres-crash-restart'
        $runner = Start-Owned ((Get-Command pwsh -CommandType Application | Select-Object -First 1).Source) @('-NoProfile','-NonInteractive','-File',
            (Join-Path $PSScriptRoot 'test-owned-postgres.ps1'), '-ArchivePath',
            $PostgresArchivePath,
            '-Scenario','NativeLifecycle','-BrokerFixtureRoot',$proof) @{} 'financial-postgres'
        $null = $runner.Process.WaitForExitAsync().WaitAsync([TimeSpan]::FromSeconds(600)).GetAwaiter().GetResult()
        Capture-Owned $runner
        if ($runner.Process.ExitCode -ne 0) { throw 'Actual financial lifecycle fixture failed; inspect owned logs.' }
        $pg = Get-Content -LiteralPath (Join-Path $proof 'financial-postgres.json') -Raw | ConvertFrom-Json
        $financial = Get-Content -LiteralPath (Join-Path $proof 'financial-restart.json') -Raw | ConvertFrom-Json
        if (-not $pg.passed -or -not $pg.cleanupSucceeded -or -not $financial.passed -or -not $financial.financialServiceWiringVerified -or
            -not $financial.postgresConvergenceVerified -or $financial.providerInvocations -ne 0 -or $financial.manualProjectorCalls -ne 0) { throw 'Financial lifecycle evidence did not pass.' }
        $result.financialServiceWiringVerified = $true; $result.postgresConvergenceVerified = $true
    } else {
    $result.stage = 'initial-fixture'
    Run-Fixture initial 540
    $result.stage = 'hard-crash'
    Stop-Owned $broker
    $result.stage = 'restart-broker'
    $broker = Start-Owned (Join-Path $erlang 'bin/erl.exe') $arguments $environment 'broker-restart'
    Wait-Listener $amqpPort $broker 90
    $result.stage = 'restart-fixture'
    Run-Fixture restart 90
    $result.stage = 'host-worker-connect'
    $reconnect = Run-Fixture -Mode reconnect -StartOnly
    $until = [DateTimeOffset]::UtcNow.AddSeconds(60)
    while (-not (Test-Path -LiteralPath (Join-Path $proof 'reconnect-ready.json'))) {
        if ($reconnect.Process.HasExited -or [DateTimeOffset]::UtcNow -gt $until) { throw 'Host worker readiness failed.' }
        Start-Sleep -Milliseconds 250
    }
    $result.stage = 'host-worker-broker-crash'
    Stop-Owned $broker
    $broker = Start-Owned (Join-Path $erlang 'bin/erl.exe') $arguments $environment 'broker-reconnect'
    Wait-Listener $amqpPort $broker 90
    [IO.File]::WriteAllText((Join-Path $proof 'broker-restarted'), '1')
    $result.stage = 'host-worker-reconnected'
    $null = $reconnect.Process.WaitForExitAsync().WaitAsync([TimeSpan]::FromSeconds(90)).GetAwaiter().GetResult()
    Capture-Owned $reconnect
    if ($reconnect.Process.ExitCode -ne 0) { throw 'Host worker automatic reconnect failed; inspect owned logs.' }
    }
    $result.passed = $true
} finally {
    foreach ($listener in $reservations) { $listener.Stop() }
    $clean = $true
    for ($index = $owned.Count - 1; $index -ge 0; $index--) {
        try { Stop-Owned $owned[$index] } catch { $clean = $false }
    }
    if ($Scenario -eq 'Financial' -and (Test-Path -LiteralPath (Join-Path $proof 'financial-postgres-location.json'))) {
        try {
            $location = Get-Content -LiteralPath (Join-Path $proof 'financial-postgres-location.json') -Raw | ConvertFrom-Json
            $pgRun = Assert-OwnedPath $location.run
            $pgParent = [IO.Path]::GetFullPath((Join-Path $runtime 'persistence-test'))
            $nonce = [guid]::Empty
            if ([IO.Path]::GetDirectoryName($pgRun) -ne $pgParent -or -not [guid]::TryParseExact([IO.Path]::GetFileName($pgRun),'N',[ref]$nonce)) { throw 'Invalid owned PostgreSQL recovery path.' }
            $pidFile = Join-Path $pgRun 'data/postmaster.pid'
            if (Test-Path -LiteralPath $pidFile) {
                $server = Get-Process -Id ([int](Get-Content -LiteralPath $pidFile -TotalCount 1)) -ErrorAction Stop
                if ($server.Path -ne (Join-Path $pgRun 'pgsql/bin/postgres.exe') -or $server.StartTime.ToUniversalTime() -lt ([DateTimeOffset]$location.startedAt).UtcDateTime) { throw 'PostgreSQL recovery ownership mismatch.' }
                $recovery = Start-Owned (Join-Path $pgRun 'pgsql/bin/pg_ctl.exe') @('-D',(Join-Path $pgRun 'data'),'-m','fast','-w','stop') @{} 'financial-postgres-recovery'
                $null = $recovery.Process.WaitForExitAsync().WaitAsync([TimeSpan]::FromSeconds(30)).GetAwaiter().GetResult()
                Capture-Owned $recovery
                if ($recovery.Process.ExitCode -ne 0 -or (Test-Path -LiteralPath $pidFile)) { throw 'Owned PostgreSQL recovery failed.' }
            }
            $passwordFile = Assert-OwnedPath (Join-Path $pgRun 'fixture-password.txt')
            if (Test-Path -LiteralPath $passwordFile) { Remove-Item -LiteralPath $passwordFile -Force }
        } catch { $clean = $false }
    }
    $result.cleanupSucceeded = $clean
    if ($clean -and (Test-Path -LiteralPath $proof)) {
        foreach ($relative in @('credentials.json','definitions.json','home/.erlang.cookie','appdata/erlang/.erlang.cookie','erl-crash.dump')) {
            $target = Assert-OwnedPath (Join-Path $proof $relative)
            if (-not $target.StartsWith(([IO.Path]::GetFullPath($proof).TrimEnd('\') + '\'), [StringComparison]::OrdinalIgnoreCase)) { throw 'Secret cleanup path escaped owned proof.' }
            if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Force }
        }
        $result.temporaryCredentialFilesRemoved = $true
    }
    foreach ($entry in $owned) { $entry.Process.Dispose() }
    $result.verifiedAt = [DateTimeOffset]::UtcNow.ToString('O')
    if (Test-Path -LiteralPath $proof) { $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $proof 'result.json') -Encoding utf8NoBOM }
    $result | ConvertTo-Json -Depth 8
    if (-not $clean) { throw 'Owned broker proof cleanup failed.' }
}
