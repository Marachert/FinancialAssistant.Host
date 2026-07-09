param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('get-page', 'add-footer-comment')]
    [string]$Action,

    [Parameter(Mandatory = $true)]
    [string]$PageId,

    [string]$ContentFile
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

switch ($Action) {
    'get-page' {
        Invoke-RestMethod -Method Get -Uri "$baseUrl/wiki/api/v2/pages/$PageId?body-format=storage" -Headers $headers |
            ConvertTo-Json -Depth 20
    }
    'add-footer-comment' {
        if ([string]::IsNullOrWhiteSpace($ContentFile)) {
            throw 'add-footer-comment requires ContentFile containing UTF-8 Markdown/plain text.'
        }

        $text = Get-Content -Raw -Encoding UTF8 $ContentFile
        $body = @{
            pageId = $PageId
            body = @{
                representation = 'storage'
                value = "<p>$([System.Net.WebUtility]::HtmlEncode($text))</p>"
            }
        } | ConvertTo-Json -Depth 20

        Invoke-RestMethod -Method Post -Uri "$baseUrl/wiki/api/v2/footer-comments" -Headers $headers -Body $body |
            ConvertTo-Json -Depth 20
    }
}
