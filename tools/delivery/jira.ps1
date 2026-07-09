param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('get-issue', 'search', 'transition', 'add-comment')]
    [string]$Action,

    [Parameter(Mandatory = $true)]
    [string]$Value,

    [string]$SecondaryValue
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Get-RequiredEnvironmentVariable([string]$Name) {
    $value = [Environment]::GetEnvironmentVariable($Name)
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "Required environment variable '$Name' is not configured."
    }

    return $value
}

$baseUrl = (Get-RequiredEnvironmentVariable 'ATLASSIAN_SITE_URL').TrimEnd('/')
$email = Get-RequiredEnvironmentVariable 'ATLASSIAN_EMAIL'
$token = Get-RequiredEnvironmentVariable 'ATLASSIAN_API_TOKEN'
$auth = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("${email}:${token}"))
$headers = @{
    Authorization = "Basic $auth"
    Accept = 'application/json'
    'Content-Type' = 'application/json'
}

function Test-IsTlsTransportFailure {
    param([System.Exception]$Exception)

    $current = $Exception
    while ($null -ne $current) {
        if ($current -is [System.Net.Http.HttpRequestException] -and
            $current.Message -like '*SSL connection could not be established*') {
            return $true
        }

        if ($current -is [System.Security.Authentication.AuthenticationException]) {
            return $true
        }

        if ($current.Message -like '*SEC_E_NO_CREDENTIALS*' -or
            $current.Message -like '*No credentials are available in the security package*') {
            return $true
        }

        $current = $current.InnerException
    }

    return $false
}

function Get-GitCurlPath {
    $gitExecPath = (& git --exec-path 2>$null)
    if (-not [string]::IsNullOrWhiteSpace($gitExecPath)) {
        $mingwPath = Split-Path -Parent (Split-Path -Parent $gitExecPath)
        $gitCurlPath = Join-Path $mingwPath 'bin/curl.exe'
        if (Test-Path -LiteralPath $gitCurlPath) {
            return $gitCurlPath
        }
    }

    $defaultGitCurlPath = 'C:\Program Files\Git\mingw64\bin\curl.exe'
    if (Test-Path -LiteralPath $defaultGitCurlPath) {
        return $defaultGitCurlPath
    }

    throw 'Git bundled curl.exe was not found for TLS fallback.'
}

function Invoke-CurlRequest {
    param(
        [Parameter(Mandatory = $true)][string]$Method,
        [Parameter(Mandatory = $true)][string]$Uri,
        [object]$Body
    )

    $curlPath = Get-GitCurlPath
    $arguments = @(
        '--fail-with-body',
        '--silent',
        '--show-error',
        '--request', $Method.ToUpperInvariant(),
        '--url', $Uri,
        '--header', "Authorization: Basic $auth",
        '--header', 'Accept: application/json',
        '--header', 'Content-Type: application/json'
    )

    $temporaryBodyPath = $null
    try {
        if ($null -ne $Body) {
            $temporaryBodyPath = [System.IO.Path]::GetTempFileName()
            $Body |
                ConvertTo-Json -Depth 20 |
                Set-Content -LiteralPath $temporaryBodyPath -Encoding UTF8NoBOM
            $arguments += @('--data-binary', "@$temporaryBodyPath")
        }

        $response = & $curlPath @arguments
        if ($LASTEXITCODE -ne 0) {
            throw "Git bundled curl.exe request failed with exit code $LASTEXITCODE."
        }

        $responseText = $response -join [Environment]::NewLine
        if ([string]::IsNullOrWhiteSpace($responseText)) {
            return $null
        }

        return $responseText | ConvertFrom-Json
    }
    finally {
        if ($null -ne $temporaryBodyPath -and (Test-Path -LiteralPath $temporaryBodyPath)) {
            Remove-Item -LiteralPath $temporaryBodyPath -Force
        }
    }
}

function Invoke-JiraRequest {
    param(
        [Parameter(Mandatory = $true)][string]$Method,
        [Parameter(Mandatory = $true)][string]$Path,
        [object]$Body
    )

    $parameters = @{
        Method = $Method
        Uri = "$baseUrl/rest/api/3/$Path"
        Headers = $headers
    }

    if ($null -ne $Body) {
        $parameters.Body = $Body | ConvertTo-Json -Depth 20
    }

    try {
        Invoke-RestMethod @parameters
    }
    catch {
        if (Test-IsTlsTransportFailure -Exception $_.Exception) {
            Invoke-CurlRequest -Method $Method -Uri $parameters.Uri -Body $Body
            return
        }

        throw
    }
}

switch ($Action) {
    'get-issue' {
        Invoke-JiraRequest -Method Get -Path "issue/$Value" -Body $null | ConvertTo-Json -Depth 20
    }
    'search' {
        $encoded = [Uri]::EscapeDataString($Value)
        Invoke-JiraRequest -Method Get -Path "search/jql?jql=$encoded&maxResults=50" -Body $null | ConvertTo-Json -Depth 20
    }
    'transition' {
        if ([string]::IsNullOrWhiteSpace($SecondaryValue)) {
            throw 'transition requires SecondaryValue containing the transition ID.'
        }

        Invoke-JiraRequest -Method Post -Path "issue/$Value/transitions" -Body @{
            transition = @{ id = $SecondaryValue }
        } | Out-Null

        @{ success = $true; issue = $Value; transitionId = $SecondaryValue } | ConvertTo-Json
    }
    'add-comment' {
        if ([string]::IsNullOrWhiteSpace($SecondaryValue)) {
            throw 'add-comment requires SecondaryValue containing a UTF-8 Markdown file path.'
        }

        $comment = Get-Content -Raw -Encoding UTF8 $SecondaryValue
        Invoke-JiraRequest -Method Post -Path "issue/$Value/comment" -Body @{
            body = @{
                type = 'doc'
                version = 1
                content = @(@{
                    type = 'paragraph'
                    content = @(@{ type = 'text'; text = $comment })
                })
            }
        } | ConvertTo-Json -Depth 20
    }
}
