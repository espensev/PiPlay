#Requires -Version 5.1
<#
.SYNOPSIS
  One-command answer to "am I testing the right build?" for the deployed Stable copy.

.DESCRIPTION
  Verifies the deployed PiPlay Stable copy (default E:\Dev_test_implemenations\PiPlay) against
  its own shipped manifest AND against this repo:

    1. build-info.json is present and the legacy BUILDINFO.json (if any) is identical;
    2. .piplay.publish.marker agrees with build-info.json (version/build/label/commit);
    3. every artifact listed in the manifest re-hashes clean (SHA256 + size) - post-copy integrity;
    4. the deployed PiPlay.exe FileVersion and ProductVersion match the manifest;
    5. the manifest sourceCommit exists in this repo, is HEAD, and has the expected stable tag;
    6. repo VERSION/BUILD_NUMBER and working-tree cleanliness agree with the deployed manifest.

  Prints [ OK ]/[WARN]/[FAIL] lines plus a final identity block. Default mode is fail-closed for
  release use: source drift, dirty trees, missing tags, a missing source commit, version/build
  mismatches, and manifests marked as non-release evidence exit 1. -AllowNonReleaseEvidence keeps
  artifact integrity checks strict but lets diagnostic dirty/non-release deploys exit 0 with a
  yellow verdict.
  -AllowMissingStableTag downgrades ONLY the missing expected stable tag to a warning (for the
  pre-tag step of a release publish, before the tag is created) while keeping every other release
  check fail-closed.

.EXAMPLE
  .\scripts\Verify-StableDeploy.ps1
.EXAMPLE
  .\scripts\Verify-StableDeploy.ps1 -DeployRoot 'E:\Dev_test_implemenations\PiPlay'
.EXAMPLE
  .\scripts\Verify-StableDeploy.ps1 -AllowMissingStableTag   # pre-tag release gate
#>
[CmdletBinding()]
param(
    [string]$DeployRoot = "E:\Dev_test_implemenations\PiPlay",
    [switch]$AllowNonReleaseEvidence,
    [switch]$AllowMissingStableTag
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$script:failCount = 0
$script:warnCount = 0

function Write-Ok([string]$message)   { Write-Host "[ OK ] $message" -ForegroundColor Green }
function Write-Warn2([string]$message){ Write-Host "[WARN] $message" -ForegroundColor Yellow; $script:warnCount++ }
function Write-Fail([string]$message) { Write-Host "[FAIL] $message" -ForegroundColor Red; $script:failCount++ }
function Write-ProvenanceIssue([string]$message) {
    if ($AllowNonReleaseEvidence) { Write-Warn2 $message }
    else { Write-Fail $message }
}

function Get-Sha256Hex([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
}

function Invoke-Git([string[]]$GitArgs) {
    $out = & git -C $repoRoot @GitArgs 2>$null
    if ($LASTEXITCODE -ne 0) { return $null }
    return $out
}

function Get-ObjectPropertyValue {
    param(
        [Parameter(Mandatory = $true)]$Object,
        [Parameter(Mandatory = $true)][string]$Name
    )

    $prop = $Object.PSObject.Properties[$Name]
    if ($prop) { return $prop.Value }
    return $null
}

Write-Host "--- PiPlay stable deploy verification ---" -ForegroundColor Cyan
Write-Host "Deploy root : $DeployRoot"
Write-Host "Repo root   : $repoRoot"
Write-Host ""

# 1. Manifest presence + legacy duplicate agreement.
if (-not (Test-Path -LiteralPath $DeployRoot)) { throw "Deploy root not found: $DeployRoot" }
$buildInfoPath = Join-Path $DeployRoot "build-info.json"
if (-not (Test-Path -LiteralPath $buildInfoPath)) {
    $buildInfoPath = Join-Path $DeployRoot "BUILDINFO.json"
    if (-not (Test-Path -LiteralPath $buildInfoPath)) { throw "No build-info.json/BUILDINFO.json in $DeployRoot." }
}
$buildInfo = Get-Content -LiteralPath $buildInfoPath -Raw | ConvertFrom-Json
Write-Ok "Manifest found: $buildInfoPath"

$legacyPath = Join-Path $DeployRoot "BUILDINFO.json"
$primaryPath = Join-Path $DeployRoot "build-info.json"
if ((Test-Path -LiteralPath $primaryPath) -and (Test-Path -LiteralPath $legacyPath)) {
    if ((Get-Sha256Hex $primaryPath) -eq (Get-Sha256Hex $legacyPath)) {
        Write-Ok "build-info.json and legacy BUILDINFO.json are identical."
    } else {
        Write-Fail "build-info.json and BUILDINFO.json differ - metadata drift."
    }
}

# 2. Marker agreement.
$markerPath = Join-Path $DeployRoot ".piplay.publish.marker"
$deployedUtc = $null
if (Test-Path -LiteralPath $markerPath) {
    $marker = @{}
    foreach ($line in (Get-Content -LiteralPath $markerPath)) {
        if ($line -match '^([A-Za-z]+)=(.*)$') { $marker[$Matches[1]] = $Matches[2] }
    }
    $deployedUtc = $marker["deployedUtc"]
    $pairs = @(
        @{ Name = "version";         Manifest = [string](Get-ObjectPropertyValue -Object $buildInfo -Name "version");         Marker = $marker["version"];         Optional = $false },
        @{ Name = "buildNumber";     Manifest = [string](Get-ObjectPropertyValue -Object $buildInfo -Name "buildNumber");     Marker = $marker["buildNumber"];     Optional = $false },
        @{ Name = "publishLabel";    Manifest = [string](Get-ObjectPropertyValue -Object $buildInfo -Name "publishLabel");    Marker = $marker["publishLabel"];    Optional = $false },
        @{ Name = "sourceCommit";    Manifest = [string](Get-ObjectPropertyValue -Object $buildInfo -Name "sourceCommit");    Marker = $marker["sourceCommit"];    Optional = $false },
        @{ Name = "releaseEvidence"; Manifest = [string](Get-ObjectPropertyValue -Object $buildInfo -Name "releaseEvidence"); Marker = $marker["releaseEvidence"]; Optional = $true },
        @{ Name = "sourceDirty";     Manifest = [string](Get-ObjectPropertyValue -Object $buildInfo -Name "sourceDirty");     Marker = $marker["sourceDirty"];     Optional = $true }
    )
    $markerMismatch = $false
    foreach ($pair in $pairs) {
        if ($pair.Optional -and [string]::IsNullOrEmpty($pair.Manifest) -and [string]::IsNullOrEmpty($pair.Marker)) {
            continue
        }
        if ($pair.Marker -ne $pair.Manifest) {
            Write-Fail "Marker/manifest disagree on $($pair.Name): marker='$($pair.Marker)' manifest='$($pair.Manifest)'."
            $markerMismatch = $true
        }
    }
    if (-not $markerMismatch) { Write-Ok "Publish marker agrees with the manifest (deployed $deployedUtc)." }
} else {
    Write-Fail "No .piplay.publish.marker - cannot confirm when/how this copy was deployed."
}

# 3. Re-hash every artifact against the manifest (post-copy integrity).
$artifacts = @($buildInfo.artifactHashes)
if ($artifacts.Count -eq 0) { throw "artifactHashes is empty in $buildInfoPath." }
$hashFailures = 0
foreach ($entry in $artifacts) {
    $artifactPath = Join-Path $DeployRoot $entry.path
    if (-not (Test-Path -LiteralPath $artifactPath)) {
        Write-Fail "Missing artifact: $($entry.path)"; $hashFailures++; continue
    }
    if ((Get-Sha256Hex $artifactPath) -ne $entry.sha256) {
        Write-Fail "Hash mismatch: $($entry.path)"; $hashFailures++; continue
    }
    if ([int64](Get-Item -LiteralPath $artifactPath).Length -ne [int64]$entry.size) {
        Write-Fail "Size mismatch: $($entry.path)"; $hashFailures++
    }
}
if ($hashFailures -eq 0) {
    Write-Ok "All $($artifacts.Count) deployed artifacts re-hash clean (SHA256 + size)."
}

# 4. Deployed exe identity matches the manifest.
$deployExe = Join-Path $DeployRoot "PiPlay.exe"
if (Test-Path -LiteralPath $deployExe) {
    $fvi = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($deployExe)
    $expectedFileVersion = "$($buildInfo.version -replace '-.*$','').$($buildInfo.buildNumber)"
    if ($fvi.FileVersion -eq $expectedFileVersion) {
        Write-Ok "PiPlay.exe FileVersion $($fvi.FileVersion) matches manifest v$($buildInfo.version) b$($buildInfo.buildNumber)."
    } else {
        Write-Fail "PiPlay.exe FileVersion '$($fvi.FileVersion)' does not match manifest expectation '$expectedFileVersion'."
    }
    $manifestProductVersion = Get-ObjectPropertyValue -Object $buildInfo -Name "productVersion"
    $expectedProductVersion = if ($manifestProductVersion) {
        [string]$manifestProductVersion
    } else {
        [string]$buildInfo.version
    }
    if ($fvi.ProductVersion -eq $expectedProductVersion) {
        Write-Ok "PiPlay.exe ProductVersion $($fvi.ProductVersion) matches manifest productVersion '$expectedProductVersion'."
    } else {
        Write-Fail "PiPlay.exe ProductVersion '$($fvi.ProductVersion)' does not match manifest productVersion '$expectedProductVersion'."
    }
    if ($buildInfo.channel) {
        if ($buildInfo.channel -eq "Stable") { Write-Ok "Manifest channel is Stable." }
        else { Write-Fail "Manifest channel is '$($buildInfo.channel)', expected Stable." }
    }
} else {
    Write-Fail "Deployed PiPlay.exe not found at $deployExe."
}

$manifestReleaseEvidence = Get-ObjectPropertyValue -Object $buildInfo -Name "releaseEvidence"
if ($null -ne $manifestReleaseEvidence) {
    if ($manifestReleaseEvidence -eq $true) {
        Write-Ok "Manifest marks this deploy as release evidence."
    } else {
        $releaseEvidenceReason = Get-ObjectPropertyValue -Object $buildInfo -Name "releaseEvidenceReason"
        $reason = if ($releaseEvidenceReason) { " Reason: $releaseEvidenceReason" } else { "" }
        Write-ProvenanceIssue "Manifest marks this deploy as NOT release evidence.$reason"
    }
} else {
    Write-ProvenanceIssue "Manifest has no releaseEvidence field; republish with the Phase 0 provenance pipeline before release QA."
}

$manifestSourceDirty = Get-ObjectPropertyValue -Object $buildInfo -Name "sourceDirty"
if ($manifestSourceDirty -eq $true) {
    $manifestSourceDirtyEntries = @(Get-ObjectPropertyValue -Object $buildInfo -Name "sourceDirtyEntries")
    $dirtyCount = $manifestSourceDirtyEntries.Count
    Write-ProvenanceIssue "Manifest says the source tree was dirty when metadata was written ($dirtyCount path(s))."
}

# 5. Cross-check sourceCommit against this repo.
$commit = [string](Get-ObjectPropertyValue -Object $buildInfo -Name "sourceCommit")
$behindCount = $null
$headShort = $null
if (-not $commit) {
    # Fail-closed for release: a deploy that cannot be tied back to a commit is not release evidence.
    # -AllowNonReleaseEvidence downgrades this to a warning for diagnostics.
    Write-ProvenanceIssue "Manifest has no sourceCommit (built without git?); cannot link deploy to a commit."
} elseif (-not (Invoke-Git @("rev-parse", "--verify", "--quiet", "$commit^{commit}"))) {
    Write-Fail "sourceCommit $commit is NOT a commit in this repo - wrong repo, or history rewritten."
} else {
    $headFull = Invoke-Git @("rev-parse", "HEAD")
    $headShort = Invoke-Git @("rev-parse", "--short", "HEAD")
    $commitShort = Invoke-Git @("rev-parse", "--short", $commit)
    if ($headFull -eq $commit) {
        Write-Ok "Deploy was built from the CURRENT HEAD ($headShort)."
    } else {
        & git -C $repoRoot merge-base --is-ancestor $commit HEAD 2>$null
        if ($LASTEXITCODE -eq 0) {
            $behindCount = Invoke-Git @("rev-list", "--count", "$commit..HEAD")
            Write-ProvenanceIssue "Deploy commit $commitShort is an ancestor of HEAD ($headShort) - deploy is $behindCount commit(s) behind. Re-publish to test the latest code."
        } else {
            Write-ProvenanceIssue "Deploy commit $commitShort is not on the current branch (HEAD $headShort) - built from another branch?"
        }
    }
    $expectedTag = "stable-v$($buildInfo.version)-b$($buildInfo.buildNumber)"
    $tags = @(Invoke-Git @("tag", "--points-at", $commit))
    if ($tags -contains $expectedTag) {
        Write-Ok "Commit has expected stable tag: $expectedTag."
    } else {
        $tagText = if ($tags.Count -gt 0) { " Existing tags: $($tags -join ', ')." } else { "" }
        if ($AllowMissingStableTag) {
            # Pre-tag step of a release publish: the tag is created only after this gate passes.
            # A warning (not a pass) so the verdict can never read RELEASE VERIFIED before tagging.
            Write-Warn2 "Expected stable tag '$expectedTag' not yet created (pre-tag verification).$tagText"
        } else {
            Write-ProvenanceIssue "Expected stable tag '$expectedTag' is missing from $commitShort.$tagText"
        }
    }
}

# 6. Repo working-tree context (how the deploy relates to what you would build NOW).
$dirty = Invoke-Git @("status", "--porcelain")
if ($dirty) {
    Write-ProvenanceIssue "Repo working tree is DIRTY ($(@($dirty).Count) path(s)) - a build made now would not match any commit."
} else {
    Write-Ok "Repo working tree is clean."
}
$repoVersion = (Get-Content -LiteralPath (Join-Path $repoRoot "VERSION") -Raw).Trim()
$repoBuildNumber = (Get-Content -LiteralPath (Join-Path $repoRoot "BUILD_NUMBER") -Raw).Trim()
if ($repoVersion -eq [string]$buildInfo.version -and $repoBuildNumber -eq [string]$buildInfo.buildNumber) {
    Write-Ok "Repo VERSION/BUILD_NUMBER ($repoVersion/b$repoBuildNumber) match the deploy."
} else {
    Write-ProvenanceIssue "Repo VERSION/BUILD_NUMBER ($repoVersion/b$repoBuildNumber) differ from the deploy (v$($buildInfo.version)/b$($buildInfo.buildNumber))."
}

# Identity block + verdict.
Write-Host ""
Write-Host "--- Deployed build identity ---" -ForegroundColor Cyan
Write-Host "Version : v$($buildInfo.version) (build $($buildInfo.buildNumber)), channel $($buildInfo.channel)"
Write-Host "Label   : $($buildInfo.publishLabel)"
Write-Host "Commit  : $commit"
Write-Host "Exe SHA : $($buildInfo.sha256)"
Write-Host "Built   : $($buildInfo.builtUtc)   Deployed: $deployedUtc"
if ($behindCount) { Write-Host "Drift   : $behindCount commit(s) behind HEAD ($headShort)" -ForegroundColor Yellow }

Write-Host ""
if ($script:failCount -gt 0) {
    Write-Host "VERDICT: FAIL - $($script:failCount) check(s) failed, $($script:warnCount) warning(s). Do NOT trust this deploy for testing." -ForegroundColor Red
    exit 1
} elseif ($script:warnCount -gt 0) {
    Write-Host "VERDICT: VERIFIED FOR DIAGNOSTICS ONLY - $($script:warnCount) warning(s). This is NOT release evidence; do not use it for release-candidate QA." -ForegroundColor Yellow
    exit 0
} else {
    Write-Host "VERDICT: RELEASE VERIFIED - you are testing v$($buildInfo.version) b$($buildInfo.buildNumber) @ $commit, current with HEAD and tagged." -ForegroundColor Green
    exit 0
}
