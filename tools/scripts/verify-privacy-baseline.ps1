#Requires -Version 7.0
[CmdletBinding()]
param(
    [string]$RepositoryRoot
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Add-PrivacyViolation {
    param(
        [System.Collections.Generic.List[string]]$Violations,
        [string]$Path,
        [string]$Rule,
        [int]$Line = 0
    )

    $location = if ($Line -gt 0) { "$Path:$Line" } else { $Path }
    $Violations.Add("$location - $Rule")
}

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Join-Path $PSScriptRoot "../.."
}

$resolvedRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
if (-not (Test-Path -LiteralPath (Join-Path $resolvedRoot ".git"))) {
    throw "Repository root does not contain a .git directory: $resolvedRoot"
}

$git = Get-Command git -ErrorAction Stop
$violations = [System.Collections.Generic.List[string]]::new()

$forbiddenDirectoryNames = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]@(
        "BuildResults",
        "TestResults",
        "bin",
        "coverage",
        "DerivedData",
        "node_modules",
        "obj",
        "Pods"
    ),
    [System.StringComparer]::OrdinalIgnoreCase
)

$forbiddenExtensions = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]@(
        ".aab",
        ".apk",
        ".binlog",
        ".cer",
        ".crt",
        ".db",
        ".dll",
        ".exe",
        ".ipa",
        ".jks",
        ".key",
        ".keystore",
        ".mobileprovision",
        ".nupkg",
        ".p12",
        ".pdb",
        ".pem",
        ".pfx",
        ".snupkg",
        ".sqlite",
        ".sqlite3",
        ".trx"
    ),
    [System.StringComparer]::OrdinalIgnoreCase
)

$textExtensions = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]@(
        ".config",
        ".cs",
        ".csproj",
        ".env",
        ".example",
        ".http",
        ".js",
        ".json",
        ".jsx",
        ".md",
        ".mjs",
        ".props",
        ".ps1",
        ".sh",
        ".targets",
        ".ts",
        ".tsx",
        ".txt",
        ".xml",
        ".yaml",
        ".yml"
    ),
    [System.StringComparer]::OrdinalIgnoreCase
)

$secretPatterns = @(
    @{
        Name = "private key material"
        Pattern = "-----BEGIN " + "(?:RSA |EC |OPENSSH )?" + "PRIVATE KEY-----"
    },
    @{
        Name = "GitHub personal access token"
        Pattern = "g" + "hp_[A-Za-z0-9]{30,}"
    },
    @{
        Name = "GitHub fine-grained personal access token"
        Pattern = "github_" + "pat_[A-Za-z0-9_]{50,}"
    },
    @{
        Name = "OpenAI API token"
        Pattern = "s" + "k-[A-Za-z0-9_-]{20,}"
    },
    @{
        Name = "AWS access key"
        Pattern = "AK" + "IA[0-9A-Z]{16}"
    }
)

$credentialAssignmentPattern =
    '(?im)\b(?:api[_-]?key|access[_-]?token|client[_-]?secret|password)\b\s*[:=]\s*["'']?(?<value>[A-Za-z0-9_./+=:-]{20,})'
$allowedSyntheticValuePattern =
    '(?i)(example|sample|test|fake|dummy|placeholder|synthetic|changeme|replace|local-only|not-a-secret)'

Push-Location $resolvedRoot
try {
    $trackedFiles = @(& $git.Source ls-files)
    if ($LASTEXITCODE -ne 0) {
        throw "git ls-files failed with exit code $LASTEXITCODE."
    }

    foreach ($relativePath in $trackedFiles) {
        $normalizedPath = $relativePath.Replace("\", "/")
        $segments = $normalizedPath.Split("/", [System.StringSplitOptions]::RemoveEmptyEntries)
        if ($segments.Count -eq 0) {
            continue
        }

        foreach ($segment in $segments) {
            if ($forbiddenDirectoryNames.Contains($segment)) {
                Add-PrivacyViolation $violations $normalizedPath "tracked generated or sensitive directory '$segment'"
                break
            }
        }

        $fileName = $segments[-1]
        $isEnvironmentFile =
            $fileName.Equals(".env", [System.StringComparison]::OrdinalIgnoreCase) -or
            $fileName.StartsWith(".env.", [System.StringComparison]::OrdinalIgnoreCase)
        $isEnvironmentExample =
            $fileName.Equals(".env.example", [System.StringComparison]::OrdinalIgnoreCase) -or
            $fileName.EndsWith(".example", [System.StringComparison]::OrdinalIgnoreCase)

        if ($isEnvironmentFile -and -not $isEnvironmentExample) {
            Add-PrivacyViolation $violations $normalizedPath "tracked local environment file"
        }

        if (
            $fileName.Equals("secrets.json", [System.StringComparison]::OrdinalIgnoreCase) -or
            $fileName.Equals("appsettings.Production.json", [System.StringComparison]::OrdinalIgnoreCase) -or
            $fileName.EndsWith(".Local.json", [System.StringComparison]::OrdinalIgnoreCase)
        ) {
            Add-PrivacyViolation $violations $normalizedPath "tracked secret or production/local configuration file"
        }

        $extension = [System.IO.Path]::GetExtension($fileName)
        $isPemExample =
            $fileName.EndsWith(".example.pem", [System.StringComparison]::OrdinalIgnoreCase) -or
            $fileName.EndsWith(".pem.example", [System.StringComparison]::OrdinalIgnoreCase)

        if ($forbiddenExtensions.Contains($extension) -and -not $isPemExample) {
            Add-PrivacyViolation $violations $normalizedPath "tracked credential, generated, or user-data artifact"
        }

        if (-not $textExtensions.Contains($extension)) {
            continue
        }

        $fullPath = Join-Path $resolvedRoot $relativePath
        if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
            Add-PrivacyViolation $violations $normalizedPath "tracked file is missing from the checkout"
            continue
        }

        $content = [System.IO.File]::ReadAllText($fullPath)

        foreach ($secretPattern in $secretPatterns) {
            foreach ($match in [regex]::Matches($content, $secretPattern.Pattern)) {
                $line = 1 + [regex]::Matches($content.Substring(0, $match.Index), "\n").Count
                Add-PrivacyViolation $violations $normalizedPath "possible $($secretPattern.Name)" $line
            }
        }

        foreach ($match in [regex]::Matches($content, $credentialAssignmentPattern)) {
            $value = $match.Groups["value"].Value
            if ($value -notmatch $allowedSyntheticValuePattern) {
                $line = 1 + [regex]::Matches($content.Substring(0, $match.Index), "\n").Count
                Add-PrivacyViolation $violations $normalizedPath "possible hard-coded credential assignment" $line
            }
        }
    }
}
finally {
    Pop-Location
}

if ($violations.Count -gt 0) {
    Write-Error "Privacy baseline failed. Matched values are intentionally omitted."
    foreach ($violation in $violations | Sort-Object -Unique) {
        Write-Error "  $violation"
    }

    exit 1
}

Write-Host "Privacy baseline passed for $($trackedFiles.Count) tracked files."
