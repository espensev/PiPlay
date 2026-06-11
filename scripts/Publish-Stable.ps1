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
    5. writes a .piplay.publish.marker;
    6. re-verifies the DEPLOYED copy via scripts\Verify-StableDeploy.ps1 (post-copy artifact
       re-hash + repo cross-check) and prints a summary.

  The deployed copy at the deploy root is the ONLY sanctioned target for manual/human testing
  (root CLAUDE.md, docs\AGENTS.md). Normal publishes bump VERSION/BUILD_NUMBER in the working
  tree; commit those stamps and tag the source commit stable-vX.Y.Z-bN. For an exact current-HEAD
  deploy, pre-commit the stamps and publish with -NoVersionBump -NoBuildNumberBump.

  By default the semantic VERSION bumps by patch before publish, so the versioned publish folder,
  archive, metadata, and window title advance together. Pass -Version minor|major|<semver> for a
  different bump, or -NoVersionBump to keep VERSION unchanged and only bump BUILD_NUMBER. If
  VERSION/BUILD_NUMBER are already committed for an exact source identity, pass both
  -NoVersionBump and -NoBuildNumberBump.
  Code signing is intentionally not part of this pipeline yet (mirrors Build-PiPlay.ps1).

.EXAMPLE
  .\scripts\Publish-Stable.ps1
.EXAMPLE
  .\scripts\Publish-Stable.ps1 -Version minor
.EXAMPLE
  .\scripts\Publish-Stable.ps1 -NoVersionBump
.EXAMPLE
  .\scripts\Publish-Stable.ps1 -NoVersionBump -NoBuildNumberBump
.EXAMPLE
  .\scripts\Publish-Stable.ps1 -DeployRoot 'E:\Dev_test_implemenations\PiPlay' -SkipTests
#>
[CmdletBinding()]
param(
    [string]$DeployRoot = "E:\Dev_test_implemenations\PiPlay",
    [string]$Version,
    [switch]$NoVersionBump,
    [int]$BuildNumber = 0,
    [switch]$NoBuildNumberBump,
    [switch]$SkipTests,
    [switch]$SkipDeploy,
    [ValidateRange(1, 200)]
    [int]$KeepPublishCount = 3
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not [System.IO.Path]::IsPathRooted($DeployRoot)) {
    # A bare token like '--help' binds positionally to -DeployRoot and would deploy a full
    # publish tree into a junk folder next to this script. Use Get-Help for usage.
    throw "DeployRoot must be an absolute path (got '$DeployRoot'). For usage, run: Get-Help $PSCommandPath"
}

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

if ($Version -and $NoVersionBump) {
    throw "Use either -Version or -NoVersionBump, not both."
}
if ($PSBoundParameters.ContainsKey("BuildNumber") -and $NoBuildNumberBump) {
    throw "Use either -BuildNumber or -NoBuildNumberBump, not both."
}

# 1. Test gate (mirror CI's deterministic lane).
if ($SkipTests) {
    Write-Step 1 "Test gate skipped (-SkipTests)."
} else {
    Write-Step 1 "Running deterministic test lane (gate)..."
    $prevDataRoot = $env:PIPLAY_DATA_ROOT
    $testDataRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("PiPlayStablePublishTests-" + [guid]::NewGuid().ToString("N"))
    $env:PIPLAY_DATA_ROOT = $testDataRoot
    try {
        & dotnet test (Join-Path $repoRoot "PiPlay.sln") --configuration Debug
        if ($LASTEXITCODE -ne 0) { throw "Test lane failed; aborting stable publish." }
    } finally {
        $env:PIPLAY_DATA_ROOT = $prevDataRoot
        if (Test-Path -LiteralPath $testDataRoot) {
            Remove-Item -LiteralPath $testDataRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

# 2. Build + publish the Stable channel Release.
Write-Step 2 "Building + publishing the Stable channel Release..."

# Free only build-tree locks: stop PiPlay processes running from THIS repo's build/publish output so the
# build can overwrite them, but leave a side-by-side dev app (installed / run from elsewhere) and the
# deployed stable copy running. Build-PiPlay's own stop is blunt (every PiPlay.exe by name), so we disable
# it below (StopProcessName = '') and scope the stop here, mirroring the deploy step's path-scoped stop.
$repoRootPrefix = $repoRoot.TrimEnd('\') + '\'
$stoppedBuildTreeInstance = $false
foreach ($proc in @(Get-Process -Name $projectName -ErrorAction SilentlyContinue)) {
    try {
        if ($proc.Path -and $proc.Path.StartsWith($repoRootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            Write-Host "  Stopping repo build-tree instance (pid $($proc.Id))..." -ForegroundColor DarkGray
            $proc | Stop-Process -Force -ErrorAction SilentlyContinue
            $stoppedBuildTreeInstance = $true
        }
    } catch { <# Path may be inaccessible; ignore and continue #> }
}
if ($stoppedBuildTreeInstance) { Start-Sleep -Milliseconds 400 }

# Hashtable splat = named parameters (an array splat would pass them positionally).
$buildParams = @{
    Stage = "Release"
    Channel = "Stable"
    KeepPublishCount = $KeepPublishCount
    StopProcessName = ""          # don't let Build-PiPlay kill every PiPlay.exe; the dev app survives a publish
}
if ($NoVersionBump) {
    $buildParams["NoVersionBump"] = $true
} else {
    # Stable publishes should normally move the versioned folder/archive/title forward.
    $buildParams["Version"] = if ($Version) { $Version } else { "patch" }
}
if ($PSBoundParameters.ContainsKey("BuildNumber")) {
    $buildParams["BuildNumber"] = $BuildNumber
} elseif ($NoBuildNumberBump) {
    $buildParams["NoBuildNumberBump"] = $true
}
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

# 5. Marker.
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

# 6. Post-copy verification: re-hash the deployed artifacts and cross-check identity vs the repo.
Write-Step 6 "Verifying the deployed copy (Verify-StableDeploy.ps1)..."
& (Join-Path $PSScriptRoot "Verify-StableDeploy.ps1") -DeployRoot $DeployRoot
if ($LASTEXITCODE -ne 0) { throw "Deployed copy failed verification - do NOT test from it." }

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
