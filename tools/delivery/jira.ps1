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

    Invoke-RestMethod @parameters
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
