#Requires -Version 5.1
<#
.SYNOPSIS
  Build, verify, and deploy a stable, differentiable PiPlay copy.

.DESCRIPTION
  Thin wrapper over scripts\Build-PiPlay.ps1 that:
    1. (optionally) runs the deterministic test lane as a gate;
    2. builds + publishes a Release with the Stable channel baked in - giving the deployed copy its
       own data root (PiPlayData beside the exe), its own single-instance identity, and a
       "PiPlay - Stable vX.Y.Z (bN)" title so it is differentiable from the dev app;
    3. validates the publish metadata (SHA256/size) via scripts\Test-PublishMetadata.ps1;
    4. deploys the runnable copy to a deploy root (default E:\Dev_test_implemenations\PiPlay),
       REPLACING the binaries but PRESERVING the PiPlayData runtime folder so login/session survive;
    5. writes a .piplay.publish.marker and prints a summary.

  By default the semantic VERSION is kept and only BUILD_NUMBER bumps, so repeated stable publishes
  produce v0.3.0 (b8), (b9), ... Pass -Version patch|minor|major|<semver> to bump the version.
  Code signing is intentionally not part of this pipeline yet (mirrors Build-PiPlay.ps1).

.EXAMPLE
  .\scripts\Publish-Stable.ps1
.EXAMPLE
  .\scripts\Publish-Stable.ps1 -Version patch
.EXAMPLE
  .\scripts\Publish-Stable.ps1 -DeployRoot 'E:\Dev_test_implemenations\PiPlay' -SkipTests
#>
[CmdletBinding()]
param(
    [string]$DeployRoot = "E:\Dev_test_implemenations\PiPlay",
    [string]$Version,
    [switch]$SkipTests,
    [switch]$SkipDeploy,
    [ValidateRange(1, 200)]
    [int]$KeepPublishCount = 10
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$buildScript = Join-Path $PSScriptRoot "Build-PiPlay.ps1"
$metadataScript = Join-Path $PSScriptRoot "Test-PublishMetadata.ps1"
$publishRoot = Join-Path $repoRoot "bin\publish"
$latestDir = Join-Path $publishRoot "latest"
$projectName = "PiPlay"
$dataFolderName = "PiPlayData"          # must match AppPaths' portable data folder (AppContext.BaseDirectory\PiPlayData)
$markerName = ".piplay.publish.marker"

function Write-Step([int]$n, [string]$message) { Write-Host "`n[$n] $message" -ForegroundColor Yellow }

Write-Host "--- PiPlay stable publish ---" -ForegroundColor Cyan
Write-Host "Repo root   : $repoRoot"
Write-Host "Deploy root : $DeployRoot"
Write-Host "Signing     : not configured" -ForegroundColor DarkGray

# 1. Test gate (mirror CI's deterministic lane).
if ($SkipTests) {
    Write-Step 1 "Test gate skipped (-SkipTests)."
} else {
    Write-Step 1 "Running deterministic test lane (gate)..."
    $prevDataRoot = $env:PIPLAY_DATA_ROOT
    $env:PIPLAY_DATA_ROOT = Join-Path ([System.IO.Path]::GetTempPath()) "PiPlayStablePublishTests"
    try {
        & dotnet test (Join-Path $repoRoot "PiPlay.sln") --configuration Debug
        if ($LASTEXITCODE -ne 0) { throw "Test lane failed; aborting stable publish." }
    } finally {
        $env:PIPLAY_DATA_ROOT = $prevDataRoot
    }
}

# 2. Build + publish the Stable channel Release.
Write-Step 2 "Building + publishing the Stable channel Release..."
# Hashtable splat = named parameters (an array splat would pass them positionally).
$buildParams = @{
    Stage = "Release"
    Channel = "Stable"
    KeepPublishCount = $KeepPublishCount
}
if ($Version) { $buildParams["Version"] = $Version }
else { $buildParams["NoVersionBump"] = $true }   # keep the semantic version; BUILD_NUMBER still bumps for a unique build
& $buildScript @buildParams
if ($LASTEXITCODE -ne 0) { throw "Build-PiPlay.ps1 failed (exit $LASTEXITCODE)." }

# Read what was just published (latest mirrors the newest build).
$buildInfoPath = Join-Path $latestDir "build-info.json"
if (-not (Test-Path -LiteralPath $buildInfoPath)) { throw "Expected build info not found at $buildInfoPath." }
$buildInfo = Get-Content -LiteralPath $buildInfoPath -Raw | ConvertFrom-Json
$publishLabel = $buildInfo.publishLabel

if ($buildInfo.channel -ne "Stable") {
    throw "Published build channel is '$($buildInfo.channel)', expected 'Stable'. Aborting deploy."
}

# 3. Validate publish metadata (SHA256/size integrity) for the freshly built label.
Write-Step 3 "Validating publish metadata for '$publishLabel'..."
& $metadataScript -PublishRoot $publishRoot -PublishLabel $publishLabel
if ($LASTEXITCODE -ne 0) { throw "Publish metadata validation failed." }

if ($SkipDeploy) {
    Write-Step 4 "Deploy skipped (-SkipDeploy)."
    Write-Host "`nStable build ready (not deployed): $latestDir" -ForegroundColor Green
    exit 0
}

# 4. Deploy the runnable copy, preserving the runtime data folder.
Write-Step 4 "Deploying to '$DeployRoot' (preserving $dataFolderName)..."

# Stop only the stable instance running from THIS deploy root so its exe/dlls can be replaced.
$deployExe = Join-Path $DeployRoot "$projectName.exe"
foreach ($proc in @(Get-Process -Name $projectName -ErrorAction SilentlyContinue)) {
    try {
        if ($proc.Path -and ($proc.Path -ieq $deployExe)) {
            Write-Host "  Stopping running stable instance (pid $($proc.Id))..." -ForegroundColor DarkGray
            $proc | Stop-Process -Force -ErrorAction SilentlyContinue
        }
    } catch { <# Path may be inaccessible; ignore and continue #> }
}
Start-Sleep -Milliseconds 400

New-Item -ItemType Directory -Path $DeployRoot -Force | Out-Null

# Replace binaries: remove everything except the preserved data folder + marker, then copy fresh.
foreach ($item in @(Get-ChildItem -LiteralPath $DeployRoot -Force -ErrorAction SilentlyContinue)) {
    if ($item.Name -ieq $dataFolderName -or $item.Name -ieq $markerName) { continue }
    Remove-Item -LiteralPath $item.FullName -Recurse -Force
}

Copy-Item -Path (Join-Path $latestDir "*") -Destination $DeployRoot -Recurse -Force

# 5. Marker + summary.
$nowUtc = (Get-Date).ToUniversalTime().ToString("o")
$markerText = @"
PiPlay stable publish marker (safe to clean).
project=$projectName
channel=Stable
version=$($buildInfo.version)
buildNumber=$($buildInfo.buildNumber)
publishLabel=$publishLabel
sourceCommit=$($buildInfo.sourceCommit)
deployedUtc=$nowUtc
"@
Set-Content -LiteralPath (Join-Path $DeployRoot $markerName) -Value $markerText -Encoding UTF8

Write-Host "`n--- STABLE DEPLOY COMPLETE ---" -ForegroundColor Green
Write-Host "Version      : $($buildInfo.version) (build $($buildInfo.buildNumber))"
Write-Host "Channel      : Stable"
Write-Host "Publish label: $publishLabel"
if ($buildInfo.sha256) { Write-Host "SHA256       : $($buildInfo.sha256)" }
if ($buildInfo.sourceCommit) { Write-Host "Commit       : $($buildInfo.sourceCommit)" }
Write-Host "Deployed exe : $deployExe"
Write-Host "Data folder  : $(Join-Path $DeployRoot $dataFolderName) (preserved across redeploys)"
Write-Host "`nRun it:  & '$deployExe'"
exit 0
