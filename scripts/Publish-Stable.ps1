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
    6. for a release publish, runs a PRE-TAG verification of the DEPLOYED copy
       (scripts\Verify-StableDeploy.ps1, post-copy artifact re-hash + repo cross-check), creates the
       stable-vX.Y.Z-bN tag ONLY after that passes, then runs a final full verification that requires
       the tag - so a verification failure never leaves a release-looking tag behind. Diagnostic
       publishes skip the tag and verify once in diagnostics-only mode. Prints a summary.

  The deployed copy at the deploy root is the ONLY sanctioned target for manual/human testing
  (root CLAUDE.md, docs\AGENTS.md). By default, this script is an exact-source release path:
  VERSION/BUILD_NUMBER must already be committed, the working tree must be clean, the build uses
  -NoVersionBump -NoBuildNumberBump, and the script creates/verifies stable-vX.Y.Z-bN on that
  exact source commit.

  For a non-release local test build that intentionally stamps VERSION/BUILD_NUMBER during the
  publish, pass -AllowVersionBump with -Version/-BuildNumber/-NoVersionBump as needed. For a
  dirty-tree diagnostic deploy, pass -AllowDirty. Both escape hatches are marked as NOT release
  evidence in the manifest and verifier output.

  Optional -SignScript is forwarded to Build-PiPlay.ps1 and runs before final hashes are written,
  so signed bytes can pass manifest verification without post-sign hash drift.

.EXAMPLE
  .\scripts\Publish-Stable.ps1
.EXAMPLE
  .\scripts\Publish-Stable.ps1 -SignScript .\scripts\Sign-PiPlay.ps1
.EXAMPLE
  .\scripts\Publish-Stable.ps1 -AllowVersionBump -Version minor
.EXAMPLE
  .\scripts\Publish-Stable.ps1 -DeployRoot 'E:\Dev_test_implemenations\PiPlay' -SkipTests -AllowDirty
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
    [switch]$AllowDirty,
    [switch]$AllowVersionBump,
    [string]$SignScript,
    [ValidateRange(1, 200)]
    [int]$KeepPublishCount = 3
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "NativeCommand.ps1")

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

function Invoke-Git {
    param([Parameter(Mandatory = $true)][string[]]$GitArgs)

    $out = Invoke-NativeCommandQuiet { & git -C $repoRoot @GitArgs }
    if ($LASTEXITCODE -ne 0) { return $null }
    return $out
}

function Get-GitDirtyEntries {
    $status = @(Invoke-Git @("status", "--porcelain", "--untracked-files=all"))
    return @($status | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function Assert-StableTag {
    param(
        [Parameter(Mandatory = $true)][string]$TagName,
        [Parameter(Mandatory = $true)][string]$Commit
    )

    $existing = Invoke-Git @("rev-parse", "--verify", "--quiet", "refs/tags/$TagName")
    if ($existing) {
        $existingCommit = Invoke-Git @("rev-list", "-n", "1", $TagName)
        if ($existingCommit -ne $Commit) {
            throw "Stable tag '$TagName' already exists at $existingCommit, expected $Commit."
        }
        Write-Host "  Stable tag already exists at the deploy commit: $TagName" -ForegroundColor Green
        return
    }

    & git -C $repoRoot tag $TagName $Commit
    if ($LASTEXITCODE -ne 0) { throw "Failed to create stable tag '$TagName' at $Commit." }
    Write-Host "  Created stable tag: $TagName -> $Commit" -ForegroundColor Green
}

Write-Host "--- PiPlay stable publish ---" -ForegroundColor Cyan
Write-Host "Repo root   : $repoRoot"
Write-Host "Deploy root : $DeployRoot"
if ($SignScript) { Write-Host "Signing     : external script ($SignScript)" -ForegroundColor Cyan }
else { Write-Host "Signing     : not configured" -ForegroundColor DarkGray }

if ($Version -and $NoVersionBump) {
    throw "Use either -Version or -NoVersionBump, not both."
}
if ($PSBoundParameters.ContainsKey("BuildNumber") -and $NoBuildNumberBump) {
    throw "Use either -BuildNumber or -NoBuildNumberBump, not both."
}
if ($Version -and -not $AllowVersionBump) {
    throw "-Version mutates VERSION and is only allowed with -AllowVersionBump. For release evidence, commit VERSION/BUILD_NUMBER first and run Publish-Stable.ps1 without version flags."
}
if ($PSBoundParameters.ContainsKey("BuildNumber") -and -not $AllowVersionBump) {
    throw "-BuildNumber mutates BUILD_NUMBER and is only allowed with -AllowVersionBump. For release evidence, commit VERSION/BUILD_NUMBER first and run Publish-Stable.ps1 without version flags."
}
if (($NoVersionBump -xor $NoBuildNumberBump) -and -not $AllowVersionBump) {
    throw "Exact-source stable publishes do not mutate either stamp. Use both -NoVersionBump and -NoBuildNumberBump, use no version flags, or pass -AllowVersionBump for non-release evidence."
}

$dirtyEntries = @(Get-GitDirtyEntries)
if ($dirtyEntries.Count -gt 0) {
    $preview = ($dirtyEntries | Select-Object -First 8) -join "`n    "
    $message = "Working tree is dirty ($($dirtyEntries.Count) path(s)). Release-verified stable deploys require a clean tree.`n    $preview"
    if (-not $AllowDirty) {
        throw "$message`nCommit/stash/revert those changes, or pass -AllowDirty for a diagnostic deploy that is NOT release evidence."
    }
    Write-Warning "$message`nContinuing because -AllowDirty was passed. This deploy will be marked NOT release evidence."
}
if ($AllowVersionBump) {
    Write-Warning "-AllowVersionBump was passed. VERSION/BUILD_NUMBER may be stamped after sourceCommit; this deploy will be marked NOT release evidence unless the resulting tree is committed and republished exact-source."
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
if ($AllowVersionBump) {
    if ($NoVersionBump) {
        $buildParams["NoVersionBump"] = $true
    } else {
        # Non-release diagnostic publishes may still move the versioned folder/archive/title forward.
        $buildParams["Version"] = if ($Version) { $Version } else { "patch" }
    }
    if ($PSBoundParameters.ContainsKey("BuildNumber")) {
        $buildParams["BuildNumber"] = $BuildNumber
    } elseif ($NoBuildNumberBump) {
        $buildParams["NoBuildNumberBump"] = $true
    }
} else {
    # Release evidence is exact-source by default: VERSION/BUILD_NUMBER are already committed.
    $buildParams["NoVersionBump"] = $true
    $buildParams["NoBuildNumberBump"] = $true
}
if ($SignScript) {
    $buildParams["SignScript"] = $SignScript
}
# Diagnostic escape hatches are never release evidence, even from a clean tree. Pass an explicit
# reason so Build-PiPlay records releaseEvidence=false regardless of source-tree state - this closes
# the clean no-op gap (e.g. -AllowVersionBump -NoVersionBump -NoBuildNumberBump on a clean tree).
$nonReleaseReasons = @()
if ($AllowDirty) {
    $nonReleaseReasons += "-AllowDirty diagnostic deploy: built with a dirty working tree; deployed bytes may not match any commit"
}
if ($AllowVersionBump) {
    $nonReleaseReasons += "-AllowVersionBump diagnostic publish: VERSION/BUILD_NUMBER may be stamped after sourceCommit"
}
if ($nonReleaseReasons.Count -gt 0) {
    $buildParams["NonReleaseReason"] = ($nonReleaseReasons -join "; ")
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

# Belt-and-braces: a diagnostic escape hatch must never surface as release evidence even if the
# reason plumbing above regresses.
if (($AllowDirty -or $AllowVersionBump) -and $buildInfo.releaseEvidence) {
    throw "Diagnostic publish (-AllowDirty/-AllowVersionBump) produced releaseEvidence=true; refusing to present a diagnostic deploy as release evidence."
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
releaseEvidence=$($buildInfo.releaseEvidence)
sourceDirty=$($buildInfo.sourceDirty)
signingEnabled=$($buildInfo.signing.enabled)
deployedUtc=$nowUtc
"@
Set-Content -LiteralPath (Join-Path $DeployRoot $markerName) -Value $markerText -Encoding UTF8

# 6/7/8. Verify the deployed copy, then tag. The stable tag is created ONLY after the deployed bytes
# verify clean against the repo, so a verification failure can never leave a release-looking tag.
$stableTag = "stable-v$($buildInfo.version)-b$($buildInfo.buildNumber)"
$verifyScript = Join-Path $PSScriptRoot "Verify-StableDeploy.ps1"
if ($AllowDirty -or $AllowVersionBump) {
    # Diagnostic deploy: no release tag; verify once in diagnostics-only mode.
    Write-Step 6 "Skipping stable tag for non-release evidence deploy."
    Write-Warning "No stable tag created because -AllowDirty or -AllowVersionBump was used."

    Write-Step 7 "Verifying the deployed copy (diagnostics-only)..."
    & $verifyScript -DeployRoot $DeployRoot -AllowNonReleaseEvidence
    if ($LASTEXITCODE -ne 0) { throw "Deployed copy failed verification - do NOT test from it." }
} else {
    # Pre-tag gate: every release check must pass and tolerate ONLY the not-yet-created stable tag.
    Write-Step 6 "Pre-tag verification of the deployed copy (tag '$stableTag' not yet created)..."
    & $verifyScript -DeployRoot $DeployRoot -AllowMissingStableTag
    if ($LASTEXITCODE -ne 0) { throw "Deployed copy failed pre-tag verification - not tagging; do NOT test from it." }

    # The deployed bytes match the clean repo at HEAD - now it is safe to mint the release tag.
    Write-Step 7 "Creating exact-source stable tag '$stableTag' (pre-tag verification passed)..."
    Assert-StableTag -TagName $stableTag -Commit ([string]$buildInfo.sourceCommit)

    # Final gate: full release verification with NO escape hatch; the tag must now be present.
    Write-Step 8 "Final verification (full release checks, stable tag required)..."
    & $verifyScript -DeployRoot $DeployRoot
    if ($LASTEXITCODE -ne 0) { throw "Deployed copy failed final verification - do NOT test from it." }
}

Write-Host "`n--- STABLE DEPLOY COMPLETE ---" -ForegroundColor Green
Write-Host "Version      : $($buildInfo.version) (build $($buildInfo.buildNumber))"
Write-Host "Channel      : Stable"
Write-Host "Publish label: $publishLabel"
if ($buildInfo.sha256) { Write-Host "SHA256       : $($buildInfo.sha256)" }
if ($buildInfo.sourceCommit) { Write-Host "Commit       : $($buildInfo.sourceCommit)" }
Write-Host "Release proof: $($buildInfo.releaseEvidence)"
if (-not $buildInfo.releaseEvidence) { Write-Host "              $($buildInfo.releaseEvidenceReason)" -ForegroundColor Yellow }
if (-not $AllowDirty -and -not $AllowVersionBump) { Write-Host "Stable tag   : $stableTag" }
Write-Host "Deployed exe : $deployExe"
Write-Host "Data folder  : $(Join-Path $DeployRoot $dataFolderName) (preserved across redeploys)"
Write-Host "`nRun it:  & '$deployExe'"
exit 0
