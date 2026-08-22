#Requires -Version 5.1
<#
.SYNOPSIS
  Build, verify, and deploy a stable, differentiable PiPlay copy.

.DESCRIPTION
  Thin wrapper over scripts\Build-PiPlay.ps1 that:
    0. takes a publish lock (per repo, plus per deploy root when deployment is enabled) so two
       publishes cannot interleave, and - for
       an exact-source release - PREFLIGHTS the stable tag it is about to create. A tag collision is a
       one-second failure now instead of a failure after the deployed copy has already been replaced;
    1. (optionally) runs the deterministic test lane as a gate;
    2. builds + publishes a Release with the Stable channel baked in - giving the deployed copy its
       own data root (PiPlayData beside the exe), its own single-instance identity, and a
       "PiPlay - Stable vX.Y.Z (bN)" title so it is differentiable from the dev app;
    3. validates the publish metadata (SHA256/size) via scripts\Test-PublishMetadata.ps1;
    4. deploys to -DeployRoot or PIPLAY_STABLE_ROOT via a STAGED SWAP
       (scripts\DeploySwap.ps1): the payload is copied to a sibling .staging directory and re-hashed
       there, the old payload is moved aside to a sibling .backup, and only then is the verified
       payload moved in. A corrupt copy dies before the live copy is touched; a failure mid-swap rolls
       the previous copy back; an interrupted run is completed or reversed on the next publish. The
       PiPlayData runtime folder is never moved, so login/session survive. The .piplay.publish.marker
       ships inside the payload, so it can never disagree with the bytes it describes;
    5. for a release publish, runs a PRE-TAG verification of the DEPLOYED copy
       (scripts\Verify-StableDeploy.ps1, post-copy artifact re-hash + repo cross-check), creates the
       stable-vX.Y.Z-bN tag ONLY after that passes, then runs a final full verification that requires
       the tag - so a verification failure never leaves a release-looking tag behind. Diagnostic
       publishes skip the tag and verify once in diagnostics-only mode. Prints a summary.

  The canonical candidate/acceptance/release lifecycle is defined in
  docs\PiPlay_Product_Engineering_Spec.md. Desk-candidate acceptance is not release provenance.
  By default, this script is an exact-source release path:
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
  .\scripts\Publish-Stable.ps1 -DeployRoot (Join-Path $env:LOCALAPPDATA 'PiPlayStable') -SkipTests -AllowDirty
#>
[CmdletBinding()]
param(
    [string]$DeployRoot,
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
. (Join-Path $PSScriptRoot "DeploySwap.ps1")
. (Join-Path $PSScriptRoot "PublishLock.ps1")
. (Join-Path $PSScriptRoot "StableDeployRoot.ps1")

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

    $git = Get-Command git -ErrorAction SilentlyContinue
    if (-not $git) { return $null }
    $global:LASTEXITCODE = 0
    $out = Invoke-NativeCommandQuiet { & $git.Source -C $repoRoot @GitArgs }
    $gitExitCode = $LASTEXITCODE
    if ($gitExitCode -ne 0) { return $null }
    return $out
}

function Invoke-GitRequired {
    param([Parameter(Mandatory = $true)][string[]]$GitArgs)

    $git = Get-Command git -ErrorAction SilentlyContinue
    if (-not $git) {
        throw "Git is required to query source dirty state before stable publication."
    }
    $global:LASTEXITCODE = 0
    $out = Invoke-NativeCommandQuiet { & $git.Source -C $repoRoot @GitArgs }
    $gitExitCode = $LASTEXITCODE
    if ($gitExitCode -ne 0) {
        throw "Required git status query failed (exit $gitExitCode); source cleanliness is unknown."
    }
    return $out
}

function Get-GitDirtyEntries {
    $status = @(Invoke-GitRequired @("status", "--porcelain", "--untracked-files=all"))
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

if (-not $SkipDeploy) {
    $DeployRoot = Resolve-StableDeployRoot -DeployRoot $DeployRoot
}

Write-Host "--- PiPlay stable publish ---" -ForegroundColor Cyan
Write-Host "Repo root   : $repoRoot"
if ($SkipDeploy) {
    Write-Host "Deploy root : skipped (-SkipDeploy)" -ForegroundColor DarkGray
} else {
    Write-Host "Deploy root : $DeployRoot"
}
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
    Write-Warning "-AllowVersionBump was passed. Intentional VERSION/BUILD_NUMBER changes will be captured as dirty source; this deploy is NOT release evidence and must be committed and republished exact-source for release provenance."
}

try {
# Serialize publishes before anything expensive or destructive happens. Lock acquisition itself is
# protected by this try/finally: if the repository lock succeeds and the deploy-root lock throws, the
# first lock must still be released. A mutex belongs to the thread that took it and PowerShell's console
# host outlives (and reuses) that thread. See Close-PublishLocks.
New-PublishLock -Key "repo|$repoRoot" -What "this repository ($repoRoot)" | Out-Null
if (-not $SkipDeploy) {
    New-PublishLock -Key "deploy|$DeployRoot" -What "this deploy root ($DeployRoot)" | Out-Null
}

# 0. Tag preflight. The stable tag used to be checked only AFTER the test lane, the build, and the
# destructive deploy - so a colliding tag replaced Stable and only then failed at the very last step.
# An exact-source publish knows the tag it will create up front (the stamps are already committed), so
# check it now, while nothing has been touched.
if (-not $AllowDirty -and -not $AllowVersionBump) {
    Write-Step 0 "Tag preflight (before tests, build, or deploy)..."
    $repoVersion = (Get-Content -LiteralPath (Join-Path $repoRoot "VERSION") -Raw).Trim()
    $repoBuildNumber = (Get-Content -LiteralPath (Join-Path $repoRoot "BUILD_NUMBER") -Raw).Trim()
    $expectedTag = "stable-v$repoVersion-b$repoBuildNumber"

    $headCommit = Invoke-Git @("rev-parse", "HEAD")
    if (-not $headCommit) { throw "Could not resolve HEAD; an exact-source stable publish must run inside a git repository." }

    $existingTagCommit = Invoke-Git @("rev-list", "-n", "1", $expectedTag)   # $null when the tag does not exist
    if ($existingTagCommit -and $existingTagCommit -ne $headCommit) {
        throw @"
Stable tag '$expectedTag' already exists at $existingTagCommit, but HEAD is $headCommit.
This publish would run the tests, rebuild, replace the deployed Stable copy, and only THEN fail at tag
creation. Choose the version move, edit VERSION/BUILD_NUMBER, commit the stamps, and re-run.
"@
    }
    if ($existingTagCommit) {
        Write-Host "  Tag '$expectedTag' already points at HEAD - this is an idempotent republish." -ForegroundColor Green
    } else {
        Write-Host "  Tag '$expectedTag' is free and will be created after the deploy verifies." -ForegroundColor Green
    }
}

# 1. Full source gate shared with local development and hosted CI.
if ($SkipTests) {
    Write-Step 1 "Test gate skipped (-SkipTests)."
} else {
    Write-Step 1 "Running the shared full local-CI source gate..."
    $localCiScript = Join-Path $PSScriptRoot "Test-LocalCI.ps1"
    if (-not (Test-Path -LiteralPath $localCiScript -PathType Leaf)) {
        throw "Shared local-CI gate not found: $localCiScript"
    }
    $powerShellPath = (Get-Command pwsh -ErrorAction Stop).Source
    $global:LASTEXITCODE = 0
    & $powerShellPath -NoProfile -File $localCiScript
    $localCiExitCode = $LASTEXITCODE
    if ($localCiExitCode -ne 0) {
        throw "Shared local-CI source gate failed (exit $localCiExitCode); aborting stable publish."
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
    $nonReleaseReasons += "-AllowVersionBump diagnostic publish: intentional VERSION/BUILD_NUMBER changes are captured as dirty source"
}
if ($nonReleaseReasons.Count -gt 0) {
    $buildParams["NonReleaseReason"] = ($nonReleaseReasons -join "; ")
    $buildParams["AllowDirtySource"] = $true
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
Write-Step 4 "Deploying to '$DeployRoot' (staged swap, preserving $dataFolderName)..."

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

# An earlier publish killed mid-swap leaves the old payload in a sibling backup. Complete or reverse
# that before staging anything new, so an interrupted run can never degrade into a broken install.
if (Repair-InterruptedDeploy -DeployRoot $DeployRoot -DataFolderName $dataFolderName -ExeName "$projectName.exe") {
    Write-Host "  Recovered leftovers from an interrupted publish." -ForegroundColor Yellow
}

# The marker ships inside the payload, so a rollback restores the old marker with the old bytes.
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

# Stage beside the live copy, re-hash the staged bytes, then swap with rollback (scripts\DeploySwap.ps1).
# A failed or corrupt copy now dies before the deployed copy is touched at all.
Invoke-StagedDeploy -DeployRoot $DeployRoot -SourceDir $latestDir -DataFolderName $dataFolderName `
    -MarkerName $markerName -MarkerText $markerText -ExeName "$projectName.exe" | Out-Null

# 5/6/7. Verify the deployed copy, then tag. The stable tag is created ONLY after the deployed bytes
# verify clean against the repo, so a verification failure can never leave a release-looking tag.
$stableTag = "stable-v$($buildInfo.version)-b$($buildInfo.buildNumber)"
$verifyScript = Join-Path $PSScriptRoot "Verify-StableDeploy.ps1"
if ($AllowDirty -or $AllowVersionBump) {
    # Diagnostic deploy: no release tag; verify once in diagnostics-only mode.
    Write-Step 5 "Skipping stable tag for non-release evidence deploy."
    Write-Warning "No stable tag created because -AllowDirty or -AllowVersionBump was used."

    Write-Step 6 "Verifying the deployed copy (diagnostics-only)..."
    & $verifyScript -DeployRoot $DeployRoot -AllowNonReleaseEvidence
    if ($LASTEXITCODE -ne 0) { throw "Deployed copy failed verification - do NOT test from it." }
} else {
    # Pre-tag gate: every release check must pass and tolerate ONLY the not-yet-created stable tag.
    Write-Step 5 "Pre-tag verification of the deployed copy (tag '$stableTag' not yet created)..."
    & $verifyScript -DeployRoot $DeployRoot -AllowMissingStableTag
    if ($LASTEXITCODE -ne 0) { throw "Deployed copy failed pre-tag verification - not tagging; do NOT test from it." }

    # The deployed bytes match the clean repo at HEAD - now it is safe to mint the release tag.
    Write-Step 6 "Creating exact-source stable tag '$stableTag' (pre-tag verification passed)..."
    Assert-StableTag -TagName $stableTag -Commit ([string]$buildInfo.sourceCommit)

    # Final gate: full release verification with NO escape hatch; the tag must now be present.
    Write-Step 7 "Final verification (full release checks, stable tag required)..."
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

}
finally {
    # Runs on success, on any throw, and on `exit` (PowerShell honours finally for all three). The body
    # above is deliberately left at top-level indentation: this try/finally exists only to guarantee the
    # lock release, and re-indenting 200 lines would bury the real diff.
    Close-PublishLocks
}
