$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$script:PocRoot = Split-Path -Parent $PSScriptRoot
$script:ComposeFile = Join-Path $script:PocRoot "compose.yml"
$script:RequiredEnvironmentVariables = @(
    "POSTGRES_PASSWORD",
    "RABBITMQ_PASSWORD",
    "REDIS_PASSWORD",
    "MINIO_ROOT_PASSWORD",
    "GRAFANA_ADMIN_PASSWORD",
    "IDENTITY_LOOKUP_HMAC_KEY",
    "IDENTITY_REFRESH_TOKEN_HASH_KEY",
    "IDENTITY_ACCESS_TOKEN_SIGNING_KEY",
    "IDENTITY_PROVIDER_HMAC_KEY",
    "IDENTITY_EVENT_USER_HMAC_KEY",
    "INTERNAL_GATEWAY_SHARED_SECRET",
    "INTERNAL_SERVICE_SHARED_SECRET",
    "RECEIPT_EVENT_SHARED_SECRET",
    "MONITORING_SIGNAL_SHARED_SECRET",
    "MCP_SHARED_SECRET"
)
$script:DurableVolumes = @(
    "fa-poc-postgres-data",
    "fa-poc-elasticsearch-data",
    "fa-poc-elasticsearch-snapshots",
    "fa-poc-rabbitmq-data",
    "fa-poc-redis-data",
    "fa-poc-minio-data",
    "fa-poc-prometheus-data",
    "fa-poc-grafana-data"
)

function Resolve-PocEnvironmentFile {
    param([Parameter(Mandatory)][string]$Path)

    $candidate = if ([System.IO.Path]::IsPathRooted($Path)) {
        $Path
    }
    else {
        Join-Path $script:PocRoot $Path
    }
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        throw "Environment file does not exist: $candidate"
    }

    return (Resolve-Path -LiteralPath $candidate).Path
}

function Read-PocEnvironment {
    param([Parameter(Mandatory)][string]$Path)

    $values = @{}
    foreach ($line in [System.IO.File]::ReadAllLines($Path)) {
        if ($line -match '^\s*#' -or [string]::IsNullOrWhiteSpace($line)) {
            continue
        }
        if ($line -notmatch '^([^=]+)=(.*)$') {
            throw "Invalid environment entry in $Path."
        }

        $values[$Matches[1].Trim()] = $Matches[2].Trim()
    }

    return $values
}

function Assert-PocEnvironment {
    param([Parameter(Mandatory)][string]$Path)

    $values = Read-PocEnvironment -Path $Path
    foreach ($name in $script:RequiredEnvironmentVariables) {
        $value = $values[$name]
        if ([string]::IsNullOrWhiteSpace($value) -or $value.StartsWith("REQUIRED_", [StringComparison]::Ordinal)) {
            throw "Required environment value is missing or still a placeholder: $name"
        }
        if ($value.Length -lt 32 -or $value -notmatch '^[A-Za-z0-9_-]+$') {
            throw "Required secret must contain at least 32 Base64URL-safe characters: $name"
        }
    }

    return $values
}

function Assert-DockerAvailable {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        throw "Docker CLI is required. Install Docker Engine or Docker Desktop and retry."
    }

    & docker version --format '{{.Server.Version}}' | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Docker daemon is not reachable."
    }
    & docker compose version | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Docker Compose v2 is required."
    }
}

function Invoke-PocCompose {
    param(
        [Parameter(Mandatory)][string]$EnvironmentFile,
        [Parameter(Mandatory)][string[]]$Arguments
    )

    & docker compose `
        --project-directory $script:PocRoot `
        --env-file $EnvironmentFile `
        --file $script:ComposeFile `
        @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Docker Compose command failed with exit code $LASTEXITCODE."
    }
}

function Resolve-BackupRoot {
    param([Parameter(Mandatory)][string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw "BackupRoot must not be empty."
    }
    [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetFullPath($Path)) | Out-Null
    return (Resolve-Path -LiteralPath $Path).Path
}
