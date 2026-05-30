#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$PublishRoot = (Join-Path (Split-Path -Parent $PSScriptRoot) 'bin\publish'),
    [string]$PublishLabel
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-Sha256Hex {
    param([Parameter(Mandatory = $true)][string]$Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
}

function Assert-HasProperties {
    param(
        [Parameter(Mandatory = $true)]$Object,
        [Parameter(Mandatory = $true)][string[]]$PropertyNames,
        [Parameter(Mandatory = $true)][string]$Context
    )

    foreach ($name in $PropertyNames) {
        if (-not ($Object.PSObject.Properties.Name -contains $name)) {
            throw "$Context is missing required property '$name'."
        }
    }
}

function Get-BuildInfoPath {
    param([Parameter(Mandatory = $true)][string]$DirectoryPath)

    foreach ($name in @('build-info.json', 'BUILDINFO.json')) {
        $candidate = Join-Path $DirectoryPath $name
        if (Test-Path -LiteralPath $candidate) { return $candidate }
    }

    return $null
}

if (-not (Test-Path -LiteralPath $PublishRoot)) {
    throw "Publish root not found: $PublishRoot"
}

$targets = @()
if ($PublishLabel) {
    $labelPath = Join-Path $PublishRoot $PublishLabel
    if (-not (Test-Path -LiteralPath $labelPath)) { throw "Publish label not found: $PublishLabel" }
    $targets = @($labelPath)
} else {
    $targets = @(
        Get-ChildItem -LiteralPath $PublishRoot -Directory |
            Where-Object { $_.Name -ne 'latest' -and $_.Name -ne 'archive' } |
            Sort-Object Name -Descending |
            Select-Object -ExpandProperty FullName
    )
}

if ($targets.Count -eq 0) { throw "No publish folders found under: $PublishRoot" }

foreach ($target in $targets) {
    $buildInfoPath = Get-BuildInfoPath -DirectoryPath $target
    if (-not $buildInfoPath) { throw "No build-info file found in publish folder: $target" }

    $buildInfo = Get-Content -LiteralPath $buildInfoPath -Raw | ConvertFrom-Json
    Assert-HasProperties -Object $buildInfo -PropertyNames @(
        'project', 'version', 'buildNumber', 'builtUtc', 'artifactHashes', 'sha256', 'size'
    ) -Context $buildInfoPath

    if ($buildInfo.version -notmatch '^\d+\.\d+\.\d+(-[A-Za-z0-9.\-]+)?$') {
        throw "Invalid version '$($buildInfo.version)' in $buildInfoPath."
    }
    if ($buildInfo.buildNumber -lt 0) {
        throw "Invalid buildNumber '$($buildInfo.buildNumber)' in $buildInfoPath."
    }

    $artifactHashes = @($buildInfo.artifactHashes)
    if ($artifactHashes.Count -eq 0) { throw "artifactHashes is empty in $buildInfoPath." }

    foreach ($entry in $artifactHashes) {
        Assert-HasProperties -Object $entry -PropertyNames @('path', 'size', 'sha256') -Context "artifact entry in $buildInfoPath"
        $artifactPath = Join-Path $target $entry.path
        if (-not (Test-Path -LiteralPath $artifactPath)) {
            throw "Artifact '$($entry.path)' listed in $buildInfoPath does not exist."
        }

        $actualHash = Get-Sha256Hex -Path $artifactPath
        if ($actualHash -ne $entry.sha256) {
            throw "Hash mismatch for '$($entry.path)' in $buildInfoPath. expected=$($entry.sha256) actual=$actualHash"
        }

        $actualSize = (Get-Item -LiteralPath $artifactPath).Length
        if ([int64]$actualSize -ne [int64]$entry.size) {
            throw "Size mismatch for '$($entry.path)' in $buildInfoPath. expected=$($entry.size) actual=$actualSize"
        }
    }
}

$versionTablePath = Join-Path $PublishRoot 'VERSION_TABLE.json'
if (Test-Path -LiteralPath $versionTablePath) {
    $table = Get-Content -LiteralPath $versionTablePath -Raw | ConvertFrom-Json
    Assert-HasProperties -Object $table -PropertyNames @('project', 'generatedAtUtc', 'builds', 'buildCount') -Context $versionTablePath
}

Write-Host "Publish metadata validation passed." -ForegroundColor Green
Write-Host "Publish root : $PublishRoot"
Write-Host "Folders      : $($targets.Count)"
