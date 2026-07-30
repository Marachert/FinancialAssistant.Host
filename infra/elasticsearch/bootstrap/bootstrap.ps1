[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()]
    [string] $ElasticsearchUrl = "http://localhost:9200"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$service = "identity"
$entity = "accounts"
$environment = "local"
$schemaVersion = 1
$generation = 1

$baseUri = $ElasticsearchUrl.TrimEnd("/")
$parsedUri = $null
if (-not [Uri]::TryCreate($baseUri, [UriKind]::Absolute, [ref] $parsedUri) -or
    $parsedUri.Scheme -notin @("http", "https")) {
    throw "ElasticsearchUrl must be an absolute HTTP or HTTPS URL."
}

$prefix = "fa-$environment-$service-$entity"
$templateName = "$prefix-template-v$schemaVersion"
$templatePattern = "$prefix-v$schemaVersion-*"
$physicalIndex = "{0}-v{1}-{2:D6}" -f $prefix, $schemaVersion, $generation
$readAlias = "$prefix-read"
$writeAlias = "$prefix-write"
$templatePath = Join-Path $PSScriptRoot "templates/identity-accounts-v1.json"

function Invoke-ElasticsearchJson {
    param(
        [Parameter(Mandatory)]
        [ValidateSet("Get", "Put", "Post")]
        [string] $Method,

        [Parameter(Mandatory)]
        [string] $Path,

        [object] $Body
    )

    $parameters = @{
        Method      = $Method
        Uri         = "$baseUri/$($Path.TrimStart('/'))"
        ErrorAction = "Stop"
    }

    if ($null -ne $Body) {
        $parameters.ContentType = "application/json"
        $parameters.Body = $Body | ConvertTo-Json -Depth 100 -Compress
    }

    Invoke-RestMethod @parameters
}

function Test-ElasticsearchResource {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    try {
        $headParameters = @{
            Method      = "Head"
            Uri         = "$baseUri/$($Path.TrimStart('/'))"
            ErrorAction = "Stop"
        }
        Invoke-WebRequest @headParameters | Out-Null
        return $true
    }
    catch {
        if ($null -ne $_.Exception.Response -and
            [int] $_.Exception.Response.StatusCode -eq 404) {
            return $false
        }

        throw
    }
}

if (-not (Test-Path -LiteralPath $templatePath -PathType Leaf)) {
    throw "Template file was not found: $templatePath"
}

$template = Get-Content -LiteralPath $templatePath -Raw | ConvertFrom-Json -Depth 100
$patterns = @($template.index_patterns)
if ($patterns.Count -ne 1 -or $patterns[0] -cne $templatePattern) {
    throw "Template index pattern must be exactly '$templatePattern'."
}

$null = Invoke-ElasticsearchJson -Method Put -Path "_index_template/$templateName" -Body $template

if (-not (Test-ElasticsearchResource -Path $physicalIndex)) {
    $createIndexBody = @{
        aliases = @{
            $readAlias = @{}
            $writeAlias = @{
                is_write_index = $true
            }
        }
    }

    $null = Invoke-ElasticsearchJson -Method Put -Path $physicalIndex -Body $createIndexBody
}
else {
    $aliasBody = @{
        actions = @(
            @{
                add = @{
                    index = $physicalIndex
                    alias = $readAlias
                }
            },
            @{
                add = @{
                    index          = $physicalIndex
                    alias          = $writeAlias
                    is_write_index = $true
                }
            }
        )
    }

    $null = Invoke-ElasticsearchJson -Method Post -Path "_aliases" -Body $aliasBody
}

$templateState = Invoke-ElasticsearchJson -Method Get -Path "_index_template/$templateName"
$readAliasState = Invoke-ElasticsearchJson -Method Get -Path "_alias/$readAlias"
$writeAliasState = Invoke-ElasticsearchJson -Method Get -Path "_alias/$writeAlias"

$templateNames = @($templateState.index_templates | ForEach-Object { $_.name })
if ($templateName -notin $templateNames) {
    throw "Elasticsearch did not return the expected template '$templateName'."
}

if ($physicalIndex -notin @($readAliasState.PSObject.Properties.Name)) {
    throw "Read alias '$readAlias' does not target '$physicalIndex'."
}

if ($physicalIndex -notin @($writeAliasState.PSObject.Properties.Name)) {
    throw "Write alias '$writeAlias' does not target '$physicalIndex'."
}

$writeIndexState = $writeAliasState.PSObject.Properties[$physicalIndex].Value
$writeAliasStateValue = $writeIndexState.aliases.PSObject.Properties[$writeAlias].Value
if ($writeAliasStateValue.is_write_index -ne $true) {
    throw "Write alias '$writeAlias' is not marked as the write index."
}

[pscustomobject] @{
    Template      = $templateName
    PhysicalIndex = $physicalIndex
    ReadAlias     = $readAlias
    WriteAlias    = $writeAlias
    Result        = "verified"
}
