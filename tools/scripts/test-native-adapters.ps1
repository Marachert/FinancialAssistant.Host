#Requires -Version 7.0
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows) { throw 'The native adapter fixture requires Windows; no platform skip or installed acceptance is inferred.' }
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$project = Join-Path $repo 'backend/shared/native-files/FinancialAssistant.NativeAdapters.Integration/FinancialAssistant.NativeAdapters.Integration.csproj'
$binary = Join-Path (Split-Path -Parent $project) 'bin/Release/net8.0/FinancialAssistant.NativeAdapters.Integration.dll'
if (-not (Test-Path -LiteralPath $binary -PathType Leaf)) { throw 'Build the Release native adapter integration project first.' }
& dotnet $binary
if ($LASTEXITCODE -ne 0) { throw 'Owned native adapter crash/restart fixture failed.' }
$run = [guid]::NewGuid().ToString('N')
$temporary = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$root = [IO.Path]::GetFullPath((Join-Path $temporary "fa-native-fixture-$run"))
$target = Join-Path $root 'target'
$junction = Join-Path $root 'junction'
$owned = $false
try {
    if (Test-Path -LiteralPath $root) { throw 'Refusing to reuse a native fixture directory.' }
    $security = [Security.AccessControl.DirectorySecurity]::new()
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    try { $sid = $identity.User; $security.SetOwner($sid) } finally { $identity.Dispose() }
    $security.SetAccessRuleProtection($true, $false)
    foreach ($value in @($sid.Value, 'S-1-5-18', 'S-1-5-32-544') | Select-Object -Unique) {
        $rule = [Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new($value),
            [Security.AccessControl.FileSystemRights]::FullControl,
            [Security.AccessControl.InheritanceFlags]'ContainerInherit,ObjectInherit',
            [Security.AccessControl.PropagationFlags]::None, [Security.AccessControl.AccessControlType]::Allow)
        $security.AddAccessRule($rule)
    }
    $null = [IO.FileSystemAclExtensions]::Create([IO.DirectoryInfo]::new($root), $security)
    $owned = $true
    [IO.File]::WriteAllText((Join-Path $root 'run.id'), $run)
    $null = [IO.FileSystemAclExtensions]::Create([IO.DirectoryInfo]::new($target), $security)
    [IO.File]::WriteAllText((Join-Path $target ([guid]::Empty.ToString('N') + '.tmp')), 'synthetic-owned-target')
    $null = New-Item -ItemType Junction -Path $junction -Target $target
    & dotnet $binary check-reparse $root $run
    if ($LASTEXITCODE -ne 0) { throw 'Owned native reparse fixture failed.' }
} finally {
    if ($owned) {
        if ($root -ne [IO.Path]::GetFullPath((Join-Path $temporary "fa-native-fixture-$run")) -or [IO.Path]::GetDirectoryName($root) -ne $temporary.TrimEnd('\','/')) {
            throw 'Unsafe native fixture cleanup path.'
        }
        if (Test-Path -LiteralPath $junction) {
            if (-not (Get-Item -LiteralPath $junction -Force).Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) { throw 'Refusing unexpected junction cleanup.' }
            Remove-Item -LiteralPath $junction -Force
        }
        if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
    }
}
