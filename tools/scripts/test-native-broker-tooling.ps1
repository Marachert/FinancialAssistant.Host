param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$names = @('prepare-native-broker-fixture.ps1','test-native-erlang.ps1','test-native-broker.ps1','test-owned-postgres.ps1')
$sources = @{}
foreach ($name in $names) {
    $path = Join-Path $PSScriptRoot $name
    $tokens = $null; $errors = $null
    $ast = [Management.Automation.Language.Parser]::ParseFile($path, [ref]$tokens, [ref]$errors)
    if ($errors.Count -ne 0) { throw "PowerShell parser rejected $name." }
    $sources[$name] = $ast.Extent.Text
    $commands = $ast.FindAll({ param($node) $node -is [Management.Automation.Language.CommandAst] }, $true)
    foreach ($command in $commands) {
        if ($command.GetCommandName() -in @('cmd','cmd.exe','wsl','wsl.exe','msiexec','msiexec.exe','New-Service','Start-Service',
            'Set-Service','Set-ItemProperty','Invoke-WebRequest','Invoke-RestMethod','Start-BitsTransfer')) {
            throw "Unexpected installation, machine mutation or network command in $name."
        }
        if ($command.GetCommandName() -eq 'Set-Content' -and @($command.CommandElements | Where-Object {
            $_ -is [Management.Automation.Language.CommandParameterAst] -and $_.ParameterName -eq 'NoClobber'
        }).Count -gt 0) { throw 'Set-Content does not support NoClobber; use an atomic create-new stream.' }
    }
}
function Require([string] $Name, [string] $Text) {
    if (-not $sources[$Name].Contains($Text, [StringComparison]::Ordinal)) { throw "Required native fixture safety boundary missing in $Name." }
}
Require 'prepare-native-broker-fixture.ps1' 'Get-AuthenticodeSignature'
Require 'prepare-native-broker-fixture.ps1' 'FileShare]::Read'
Require 'prepare-native-broker-fixture.ps1' 'Unsafe package entry path.'
Require 'test-native-erlang.ps1' 'StartTime.ToUniversalTime() -ne $startedAt'
Require 'test-native-erlang.ps1' "'-start_epmd','false'"
Require 'test-native-broker.ps1' 'SetAccessRuleProtection($true, $false)'
Require 'test-native-broker.ps1' 'process.StartTime.ToUniversalTime() -ne $Entry.StartedAt'
Require 'test-native-broker.ps1' 'financial-postgres-location.json'
Require 'test-native-broker.ps1' 'manualProjectorCalls -ne 0'
Require 'test-native-broker.ps1' 'providerInvocations -ne 0'
Require 'test-native-broker.ps1' 'temporaryCredentialFilesRemoved'
Require 'test-owned-postgres.ps1' '[IO.FileMode]::CreateNew'
Require 'test-owned-postgres.ps1' '$serverProcess.Path -ne'
Require 'test-owned-postgres.ps1' "'-m','immediate'"
Require 'test-owned-postgres.ps1' 'cleanupSucceeded=$cleanupSucceeded'
Write-Output 'Native broker tooling structural guards passed. Runtime recovery is qualified only by actual isolated fixture results.'
