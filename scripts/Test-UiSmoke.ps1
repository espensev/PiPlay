#Requires -Version 7
<#
.SYNOPSIS
  Validates and, on an interactive desktop, smokes a verified Stable release or GitHub desk candidate.
.DESCRIPTION
  Release mode resolves PIPLAY_STABLE_ROOT (or -Root), runs Verify-StableDeploy.ps1 in a child
  PowerShell host, then launches that root's PiPlay.exe. DeskCandidate mode defaults to the payload
  root above this packaged scripts directory and validates both manifests, every artifact hash and
  size, provenance, channel, executable version, and packaged validation scripts before launch.
  -ValidateOnly performs the same preflight without loading UI assemblies, launching PiPlay, or
  creating screenshot evidence. A desk-candidate verdict is never release evidence.
.EXAMPLE
  pwsh -NoProfile -File .\scripts\Test-UiSmoke.ps1 -ValidateOnly
.EXAMPLE
  pwsh -NoProfile -File .\scripts\Test-UiSmoke.ps1 -Mode DeskCandidate -ValidateOnly
.EXAMPLE
  pwsh -NoProfile -File .\scripts\Test-UiSmoke.ps1 -Mode DeskCandidate -KeepOpen
#>
[CmdletBinding()]
param(
    [ValidateSet('Release', 'DeskCandidate')]
    [string]$Mode = 'Release',
    [string]$Root,
    [string]$ExePath,
    [string]$EvidenceDir,
    [ValidateRange(1, 300)]
    [int]$ReadyTimeoutSec = 30,
    [switch]$ValidateOnly,
    [switch]$KeepOpen
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'StableDeployRoot.ps1')

$script:DeskCandidateReason =
    'GitHub Actions desk candidate; final interactive verification pending on SND-DESK'

function Get-ObjectPropertyValue {
    param(
        [Parameter(Mandatory = $true)]$Object,
        [Parameter(Mandatory = $true)][string]$Name
    )

    $property = $Object.PSObject.Properties[$Name]
    if ($property) { return $property.Value }
    return $null
}

function Assert-RequiredProperties {
    param(
        [Parameter(Mandatory = $true)]$Object,
        [Parameter(Mandatory = $true)][string[]]$Names,
        [Parameter(Mandatory = $true)][string]$Context
    )

    foreach ($name in $Names) {
        if (-not ($Object.PSObject.Properties.Name -contains $name)) {
            throw "$Context is missing required property '$name'."
        }
    }
}

function Get-CanonicalPayloadFiles {
    param([Parameter(Mandatory = $true)][string]$PayloadRoot)

    $resolvedRoot = Resolve-FullyQualifiedFileSystemPath -Path $PayloadRoot -Name 'PayloadRoot'
    Assert-NoExistingReparsePointComponents -Path $resolvedRoot -Name 'PayloadRoot'
    $normalizedRoot = $resolvedRoot.Replace('/', '\').TrimEnd('\')
    $rootPrefix = $normalizedRoot + '\'
    $pending = [System.Collections.Generic.Queue[string]]::new()
    $pending.Enqueue($resolvedRoot)
    $files = [System.Collections.Generic.List[object]]::new()

    while ($pending.Count -gt 0) {
        $directory = $pending.Dequeue()
        foreach ($item in @(Get-ChildItem -LiteralPath $directory -Force -ErrorAction Stop)) {
            if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Desk candidate payload crosses reparse point '$($item.FullName)'."
            }
            if ($item.PSIsContainer) {
                $pending.Enqueue($item.FullName)
                continue
            }

            $fullPath = [System.IO.Path]::GetFullPath($item.FullName)
            $normalizedFullPath = $fullPath.Replace('/', '\').TrimEnd('\')
            if (-not $normalizedFullPath.StartsWith(
                    $rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Desk candidate payload file escapes root: '$fullPath'."
            }

            $parent = Split-Path -Parent $fullPath
            $isRootManifest = $parent.Equals(
                $resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase) -and
                $item.Name -in @('build-info.json', 'BUILDINFO.json')
            if ($isRootManifest) { continue }

            $files.Add([pscustomobject]@{
                RelativePath = $normalizedFullPath.Substring($rootPrefix.Length).Replace('\', '/')
                FullPath = $fullPath
            })
        }
    }

    return $files.ToArray()
}

function Resolve-SmokeRoot {
    if ($Mode -eq 'Release') {
        # A packaged copy travels with a root manifest and derives its "repository" as the payload
        # itself, so Release mode (repository checkout plus Verify-StableDeploy.ps1) can never
        # succeed there; fail fast with the mode that works instead of a misleading overlap error.
        $packagedManifest = Join-Path (Split-Path -Parent $PSScriptRoot) 'build-info.json'
        if (Test-Path -LiteralPath $packagedManifest -PathType Leaf) {
            throw ('This copy of Test-UiSmoke.ps1 is running from a packaged payload; Release ' +
                'mode requires the repository checkout. Run it with -Mode DeskCandidate instead.')
        }
        return Resolve-StableDeployRoot -DeployRoot $Root
    }

    $candidate = $Root
    if ([string]::IsNullOrWhiteSpace($candidate)) {
        $candidate = Split-Path -Parent $PSScriptRoot
    }
    $resolved = Resolve-FullyQualifiedFileSystemPath -Path $candidate -Name 'DeskCandidateRoot'
    Assert-NoExistingReparsePointComponents -Path $resolved -Name 'DeskCandidateRoot'
    return $resolved
}

function Resolve-DeskCandidateDataRoot {
    param(
        [Parameter(Mandatory = $true)][string]$SelectedRoot,
        [Parameter(Mandatory = $true)][string]$SourceCommit
    )

    if ($SourceCommit -notmatch '^[0-9a-fA-F]{40}$') {
        throw "Desk candidate sourceCommit must be 40 hexadecimal characters."
    }

    $configuredRoot = [Environment]::GetEnvironmentVariable(
        'PIPLAY_DESK_CANDIDATE_DATA_ROOT', 'Process')
    if ([string]::IsNullOrWhiteSpace($configuredRoot)) {
        $localAppData = [Environment]::GetEnvironmentVariable('LOCALAPPDATA', 'Process')
        if ([string]::IsNullOrWhiteSpace($localAppData)) {
            throw 'LOCALAPPDATA is required when PIPLAY_DESK_CANDIDATE_DATA_ROOT is not set.'
        }
        $configuredRoot = Join-Path `
            (Join-Path $localAppData 'PiPlay\DeskCandidate') `
            $SourceCommit.ToLowerInvariant()
    }

    $resolvedDataRoot = Resolve-FullyQualifiedFileSystemPath `
        -Path $configuredRoot -Name 'DeskCandidateDataRoot'
    Assert-NoExistingReparsePointComponents `
        -Path $resolvedDataRoot -Name 'DeskCandidateDataRoot'
    Assert-FileSystemPathsDisjoint `
        -FirstPath $resolvedDataRoot `
        -SecondPath $SelectedRoot `
        -FirstName 'DeskCandidateDataRoot' `
        -SecondName 'DeskCandidateRoot'
    return $resolvedDataRoot
}

function Resolve-SmokeEvidenceRoot {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('Release', 'DeskCandidate')]
        [string]$SmokeMode,
        [Parameter(Mandatory = $true)][string]$SelectedRoot,
        [string]$RequestedEvidenceRoot
    )

    $configuredRoot = $RequestedEvidenceRoot
    if ([string]::IsNullOrWhiteSpace($configuredRoot)) {
        $configuredRoot = [Environment]::GetEnvironmentVariable(
            'PIPLAY_UI_EVIDENCE_ROOT', 'Process')
    }
    if ([string]::IsNullOrWhiteSpace($configuredRoot)) {
        $configuredRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'PiPlayUiSmoke'
    }

    $resolvedEvidenceRoot = Resolve-FullyQualifiedFileSystemPath `
        -Path $configuredRoot -Name 'EvidenceDir'
    Assert-NoExistingReparsePointComponents `
        -Path $resolvedEvidenceRoot -Name 'EvidenceDir'
    Assert-FileSystemPathsDisjoint `
        -FirstPath $resolvedEvidenceRoot `
        -SecondPath $SelectedRoot `
        -FirstName 'EvidenceDir' `
        -SecondName "$($SmokeMode)Root"
    return $resolvedEvidenceRoot
}

function Assert-BoundExecutable {
    param([Parameter(Mandatory = $true)][string]$SelectedRoot)

    $expected = [System.IO.Path]::GetFullPath((Join-Path $SelectedRoot 'PiPlay.exe'))
    if (-not [string]::IsNullOrWhiteSpace($ExePath)) {
        $provided = Resolve-FullyQualifiedFileSystemPath -Path $ExePath -Name 'ExePath'
        if (-not $provided.Equals($expected, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "ExePath must be the PiPlay.exe under the selected root ('$expected'); got '$provided'."
        }
    }
    if (-not (Test-Path -LiteralPath $expected -PathType Leaf)) {
        throw "PiPlay.exe not found at '$expected'."
    }
    return $expected
}

function Assert-ReleasePreflight {
    param([Parameter(Mandatory = $true)][string]$SelectedRoot)

    $verifyScript = Join-Path $PSScriptRoot 'Verify-StableDeploy.ps1'
    if (-not (Test-Path -LiteralPath $verifyScript -PathType Leaf)) {
        throw "Release verification script not found: $verifyScript"
    }
    $powerShellPath = (Get-Command pwsh -ErrorAction Stop).Source
    $global:LASTEXITCODE = 0
    & $powerShellPath -NoProfile -File $verifyScript -DeployRoot $SelectedRoot
    $verifyExitCode = $LASTEXITCODE
    if ($verifyExitCode -ne 0) {
        throw "Release preflight failed in Verify-StableDeploy.ps1 (exit $verifyExitCode)."
    }
}

function Assert-DeskCandidatePreflight {
    param(
        [Parameter(Mandatory = $true)][string]$SelectedRoot,
        [Parameter(Mandatory = $true)][string]$SelectedExe
    )

    $primaryManifest = Join-Path $SelectedRoot 'build-info.json'
    $legacyManifest = Join-Path $SelectedRoot 'BUILDINFO.json'
    foreach ($manifestPath in @($primaryManifest, $legacyManifest)) {
        if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
            throw "Desk candidate requires both build-info.json and BUILDINFO.json; missing '$manifestPath'."
        }
    }
    if ((Get-Sha256Hex -Path $primaryManifest) -ine (Get-Sha256Hex -Path $legacyManifest)) {
        throw 'Desk candidate build-info.json and BUILDINFO.json are not identical.'
    }

    $buildInfo = Get-Content -LiteralPath $primaryManifest -Raw | ConvertFrom-Json
    Assert-RequiredProperties -Object $buildInfo -Context $primaryManifest -Names @(
        'project', 'version', 'buildNumber', 'publishLabel', 'channel', 'configuration',
        'sourceCommit', 'publishedArtifacts', 'primaryArtifact', 'sha256', 'size',
        'artifactCount', 'artifactHashes',
        'releaseEvidence', 'releaseEvidenceReason', 'sourceDirty', 'sourceDirtyEntries',
        'fileVersion', 'productVersion')

    if ([string](Get-ObjectPropertyValue $buildInfo 'project') -cne 'PiPlay') {
        throw "Desk candidate project must be 'PiPlay'."
    }
    if ([string](Get-ObjectPropertyValue $buildInfo 'channel') -cne 'Stable') {
        throw "Desk candidate manifest channel must be 'Stable'."
    }
    if ([string](Get-ObjectPropertyValue $buildInfo 'configuration') -cne 'Release') {
        throw "Desk candidate configuration must be 'Release'."
    }
    if ([string](Get-ObjectPropertyValue $buildInfo 'primaryArtifact') -ine 'PiPlay.exe') {
        throw "Desk candidate primaryArtifact must be 'PiPlay.exe'."
    }

    $sourceCommit = [string](Get-ObjectPropertyValue $buildInfo 'sourceCommit')
    if ($sourceCommit -notmatch '^[0-9a-fA-F]{40}$') {
        throw 'Desk candidate sourceCommit must be exactly 40 hexadecimal characters.'
    }
    $sourceDirty = Get-ObjectPropertyValue $buildInfo 'sourceDirty'
    if ($sourceDirty -isnot [bool] -or $sourceDirty) {
        throw 'Desk candidate sourceDirty must be false.'
    }
    if (@(Get-ObjectPropertyValue $buildInfo 'sourceDirtyEntries').Count -ne 0) {
        throw 'Desk candidate sourceDirtyEntries must be empty.'
    }
    $releaseEvidence = Get-ObjectPropertyValue $buildInfo 'releaseEvidence'
    if ($releaseEvidence -isnot [bool] -or $releaseEvidence) {
        throw 'Desk candidate releaseEvidence must be false.'
    }
    if ([string](Get-ObjectPropertyValue $buildInfo 'releaseEvidenceReason') -cne
        $script:DeskCandidateReason) {
        throw "Desk candidate releaseEvidenceReason must exactly equal '$script:DeskCandidateReason'."
    }

    $publishedArtifacts = @(Get-ObjectPropertyValue $buildInfo 'publishedArtifacts')
    if ($publishedArtifacts.Count -eq 0) {
        throw 'Desk candidate publishedArtifacts must not be empty.'
    }
    $publishedArtifactEntries = @(
        foreach ($publishedArtifact in $publishedArtifacts) {
            if ($publishedArtifact -isnot [string] -or
                [string]::IsNullOrWhiteSpace([string]$publishedArtifact)) {
                throw 'Desk candidate publishedArtifacts values must be non-empty paths.'
            }
            [pscustomobject]@{ path = [string]$publishedArtifact }
        }
    )
    $resolvedPublishedArtifacts = @(Resolve-ManifestArtifactPaths `
        -PayloadRoot $SelectedRoot -Entries $publishedArtifactEntries)
    if (@($resolvedPublishedArtifacts | Where-Object {
        $_.FullPath.Equals($SelectedExe, [System.StringComparison]::OrdinalIgnoreCase)
    }).Count -ne 1) {
        throw 'Desk candidate publishedArtifacts must contain the selected PiPlay.exe exactly once.'
    }

    $artifacts = @(Get-ObjectPropertyValue $buildInfo 'artifactHashes')
    if ($artifacts.Count -eq 0) { throw 'Desk candidate artifactHashes must not be empty.' }
    $artifactCount = Get-ObjectPropertyValue $buildInfo 'artifactCount'
    if (($artifactCount -isnot [long] -and $artifactCount -isnot [int]) -or
        [int64]$artifactCount -ne $artifacts.Count) {
        throw 'Desk candidate artifactCount does not match artifactHashes.'
    }
    foreach ($entry in $artifacts) {
        Assert-RequiredProperties -Object $entry -Context 'artifactHashes entry' -Names @(
            'path', 'size', 'sha256')
        $entrySize = Get-ObjectPropertyValue $entry 'size'
        if (($entrySize -isnot [long] -and $entrySize -isnot [int]) -or [int64]$entrySize -lt 0) {
            throw "Artifact '$($entry.path)' has an invalid negative size."
        }
        if ([string](Get-ObjectPropertyValue $entry 'sha256') -notmatch '^[0-9a-fA-F]{64}$') {
            throw "Artifact '$($entry.path)' has an invalid SHA256 value."
        }
    }

    $resolvedArtifacts = @(Resolve-ManifestArtifactPaths `
        -PayloadRoot $SelectedRoot -Entries $artifacts)
    $rootExecutablePaths = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase)
    foreach ($resolvedArtifact in $resolvedArtifacts) {
        $relativePath = $resolvedArtifact.RelativePath.Replace('\', '/')
        if ($relativePath -notmatch '/' -and
            [System.IO.Path]::GetExtension($relativePath) -ieq '.exe') {
            [void]$rootExecutablePaths.Add([System.IO.Path]::GetFullPath($resolvedArtifact.FullPath))
        }
    }
    $publishedExecutablePaths = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase)
    foreach ($resolvedPublishedArtifact in $resolvedPublishedArtifacts) {
        $relativePath = $resolvedPublishedArtifact.RelativePath.Replace('\', '/')
        if ($relativePath -match '/' -or
            [System.IO.Path]::GetExtension($relativePath) -ine '.exe') {
            throw "publishedArtifacts value '$relativePath' is not a top-level executable."
        }
        [void]$publishedExecutablePaths.Add(
            [System.IO.Path]::GetFullPath($resolvedPublishedArtifact.FullPath))
    }
    if (-not $publishedExecutablePaths.SetEquals($rootExecutablePaths)) {
        throw 'Desk candidate publishedArtifacts does not equal the top-level executable artifact set.'
    }

    $actualFiles = @(Get-CanonicalPayloadFiles -PayloadRoot $SelectedRoot)
    $manifestPaths = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase)
    foreach ($resolvedArtifact in $resolvedArtifacts) {
        [void]$manifestPaths.Add([System.IO.Path]::GetFullPath($resolvedArtifact.FullPath))
    }
    $actualPaths = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase)
    foreach ($actualFile in $actualFiles) {
        [void]$actualPaths.Add([System.IO.Path]::GetFullPath($actualFile.FullPath))
    }
    $unlistedFiles = @($actualFiles | Where-Object { -not $manifestPaths.Contains($_.FullPath) })
    $missingFiles = @($resolvedArtifacts | Where-Object { -not $actualPaths.Contains($_.FullPath) })
    if ($unlistedFiles.Count -gt 0 -or $missingFiles.Count -gt 0) {
        $unlistedText = @($unlistedFiles.RelativePath) -join ', '
        $missingText = @($missingFiles.RelativePath) -join ', '
        throw "Desk candidate payload/manifest file sets differ. Unlisted=[$unlistedText] Missing=[$missingText]"
    }

    $exeArtifacts = @($resolvedArtifacts | Where-Object {
        $_.FullPath.Equals($SelectedExe, [System.StringComparison]::OrdinalIgnoreCase)
    })
    if ($exeArtifacts.Count -ne 1) {
        throw 'Desk candidate artifactHashes must cover the selected PiPlay.exe exactly once.'
    }
    $exeEntry = $exeArtifacts[0].Entry

    $coveredPaths = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase)
    foreach ($resolvedArtifact in $resolvedArtifacts) {
        $entry = $resolvedArtifact.Entry
        $artifactPath = $resolvedArtifact.FullPath
        if (-not (Test-Path -LiteralPath $artifactPath -PathType Leaf)) {
            throw "Desk candidate artifact is missing: $($entry.path)"
        }
        $actualSize = [int64](Get-Item -LiteralPath $artifactPath).Length
        if ($actualSize -ne [int64]$entry.size) {
            throw "Desk candidate artifact size mismatch: $($entry.path)"
        }
        if ((Get-Sha256Hex -Path $artifactPath) -ine [string]$entry.sha256) {
            throw "Desk candidate artifact hash mismatch: $($entry.path)"
        }
        [void]$coveredPaths.Add($resolvedArtifact.RelativePath.Replace('\', '/'))
    }
    foreach ($requiredPath in @('scripts/StableDeployRoot.ps1', 'scripts/Test-UiSmoke.ps1')) {
        if (-not $coveredPaths.Contains($requiredPath)) {
            throw "Desk candidate manifest does not cover packaged validation script '$requiredPath'."
        }
    }

    $topSha256 = [string](Get-ObjectPropertyValue $buildInfo 'sha256')
    if ($topSha256 -notmatch '^[0-9a-fA-F]{64}$' -or
        $topSha256 -ine [string]$exeEntry.sha256) {
        throw 'Desk candidate top-level sha256 does not match the unique PiPlay.exe artifact.'
    }
    $topSize = Get-ObjectPropertyValue $buildInfo 'size'
    if (($topSize -isnot [long] -and $topSize -isnot [int]) -or
        [int64]$topSize -ne [int64]$exeEntry.size) {
        throw 'Desk candidate top-level size does not match the unique PiPlay.exe artifact.'
    }

    $version = [string](Get-ObjectPropertyValue $buildInfo 'version')
    $versionBase = $version -replace '-.*$', ''
    if ($versionBase -notmatch '^\d+\.\d+\.\d+$') {
        throw "Desk candidate version '$version' is invalid."
    }
    $buildNumber = Get-ObjectPropertyValue $buildInfo 'buildNumber'
    if ($buildNumber -isnot [long] -and $buildNumber -isnot [int]) {
        throw 'Desk candidate buildNumber must be an integer.'
    }
    if ([int64]$buildNumber -lt 0) { throw 'Desk candidate buildNumber must not be negative.' }

    $fileVersionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($SelectedExe)
    $expectedFileVersion = "$versionBase.$buildNumber"
    $manifestFileVersion = [string](Get-ObjectPropertyValue $buildInfo 'fileVersion')
    if ($manifestFileVersion -cne $expectedFileVersion -or
        $fileVersionInfo.FileVersion -cne $manifestFileVersion) {
        throw "PiPlay.exe FileVersion '$($fileVersionInfo.FileVersion)' does not match candidate '$expectedFileVersion'."
    }
    $manifestProductVersion = [string](Get-ObjectPropertyValue $buildInfo 'productVersion')
    if ([string]::IsNullOrWhiteSpace($manifestProductVersion) -or
        $fileVersionInfo.ProductVersion -cne $manifestProductVersion) {
        throw "PiPlay.exe ProductVersion '$($fileVersionInfo.ProductVersion)' does not match candidate '$manifestProductVersion'."
    }

    return $buildInfo
}

if ($KeepOpen -and ($Mode -ne 'DeskCandidate' -or $ValidateOnly)) {
    throw 'KeepOpen is valid only for interactive DeskCandidate smoke (without -ValidateOnly).'
}

$selectedRoot = Resolve-SmokeRoot
$selectedExe = Assert-BoundExecutable -SelectedRoot $selectedRoot
$buildInfo = $null
$candidateDataRoot = $null
if ($Mode -eq 'Release') {
    Assert-ReleasePreflight -SelectedRoot $selectedRoot
} else {
    $buildInfo = Assert-DeskCandidatePreflight -SelectedRoot $selectedRoot -SelectedExe $selectedExe
    $candidateDataRoot = Resolve-DeskCandidateDataRoot `
        -SelectedRoot $selectedRoot `
        -SourceCommit ([string]$buildInfo.sourceCommit)
}

if ($ValidateOnly) {
    if ($Mode -eq 'Release') {
        Write-Host 'VERDICT: RELEASE PREFLIGHT VERIFIED.' -ForegroundColor Green
    } else {
        Write-Host ("VERDICT: DESK CANDIDATE PREFLIGHT VERIFIED - NOT RELEASE EVIDENCE. " +
            "v$($buildInfo.version) b$($buildInfo.buildNumber) @ $($buildInfo.sourceCommit)") `
            -ForegroundColor Yellow
    }
    exit 0
}

function New-SmokeProcessStartInfo {
    param(
        [Parameter(Mandatory = $true)][string]$SelectedExe,
        [Parameter(Mandatory = $true)]
        [ValidateSet('Release', 'DeskCandidate')]
        [string]$SmokeMode,
        [string]$CandidateDataRoot
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $SelectedExe
    $startInfo.UseShellExecute = $false
    [void]$startInfo.Environment.Remove('PIPLAY_CHANNEL')
    [void]$startInfo.Environment.Remove('PIPLAY_DATA_ROOT')
    if ($SmokeMode -eq 'DeskCandidate') {
        if ([string]::IsNullOrWhiteSpace($CandidateDataRoot)) {
            throw 'CandidateDataRoot is required for DeskCandidate process launch.'
        }
        $startInfo.Environment['PIPLAY_DATA_ROOT'] = $CandidateDataRoot
    }
    return $startInfo
}

function Start-SmokeProcess {
    param(
        [Parameter(Mandatory = $true)][string]$SelectedExe,
        [Parameter(Mandatory = $true)]
        [ValidateSet('Release', 'DeskCandidate')]
        [string]$SmokeMode,
        [string]$CandidateDataRoot
    )

    $startInfo = New-SmokeProcessStartInfo `
        -SelectedExe $SelectedExe `
        -SmokeMode $SmokeMode `
        -CandidateDataRoot $CandidateDataRoot
    $process = [System.Diagnostics.Process]::Start($startInfo)
    if (-not $process) { throw "Failed to start '$SelectedExe'." }
    return $process
}

$EvidenceDir = Resolve-SmokeEvidenceRoot `
    -SmokeMode $Mode `
    -SelectedRoot $selectedRoot `
    -RequestedEvidenceRoot $EvidenceDir

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
New-Item -ItemType Directory -Force -Path $EvidenceDir | Out-Null

$proc = $null
try {
    $proc = Start-SmokeProcess `
        -SelectedExe $selectedExe `
        -SmokeMode $Mode `
        -CandidateDataRoot $candidateDataRoot
    $deadline = (Get-Date).AddSeconds($ReadyTimeoutSec)
    $automationRoot = $null
    while ((Get-Date) -lt $deadline -and -not $automationRoot) {
        Start-Sleep -Milliseconds 400
        $automationRoot = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
            [System.Windows.Automation.TreeScope]::Children,
            (New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)))
    }
    if (-not $automationRoot) {
        throw "PiPlay main window did not appear within $ReadyTimeoutSec s."
    }

    function Assert-Element([string]$AutomationId, [string]$Label) {
        $condition = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $AutomationId)
        $element = $automationRoot.FindFirst(
            [System.Windows.Automation.TreeScope]::Descendants, $condition)
        if (-not $element) { throw "MISSING UI element: $Label (AutomationId=$AutomationId)" }
        Write-Host "OK  $Label" -ForegroundColor Green
    }

    Assert-Element 'PopOutButton' 'Pop out video button'
    Assert-Element 'UrlBox' 'URL / address box'
    Assert-Element 'CloseButton' 'Close caption button'
    Assert-Element 'ProfilesCombo' 'Profiles dropdown'
    Assert-Element 'SettingsButton' 'Settings gear button'

    $bitmap = $null
    $graphics = $null
    try {
        $rect = $automationRoot.Current.BoundingRectangle
        $bitmap = [System.Drawing.Bitmap]::new([int]$rect.Width, [int]$rect.Height)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $graphics.CopyFromScreen([int]$rect.X, [int]$rect.Y, 0, 0, $bitmap.Size)
        $shot = Join-Path $EvidenceDir ("ui-smoke-{0}.png" -f (Get-Date -Format 'yyyyMMdd-HHmmss'))
        $bitmap.Save($shot, [System.Drawing.Imaging.ImageFormat]::Png)
        Write-Host "Saved screenshot: $shot" -ForegroundColor Cyan
    } finally {
        if ($graphics) { $graphics.Dispose() }
        if ($bitmap) { $bitmap.Dispose() }
    }

    if ($Mode -eq 'Release') {
        Write-Host 'AUTOMATED RELEASE DESKTOP SMOKE PASS.' -ForegroundColor Green
    } else {
        Write-Host 'AUTOMATED DESKTOP SMOKE PASS - NOT RELEASE EVIDENCE.' -ForegroundColor Yellow
    }

    if ($KeepOpen) {
        Write-Host ('PiPlay remains open for the canonical final human session. ' +
            'Close PiPlay when the session is complete; this script records no human result.') `
            -ForegroundColor Cyan
        $proc.WaitForExit()
    }
} finally {
    if ($proc) {
        try {
            if (-not $proc.HasExited) {
                $proc.CloseMainWindow() | Out-Null
                Start-Sleep -Seconds 1
                if (-not $proc.HasExited) { $proc.Kill() }
            }
        } finally {
            $proc.Dispose()
        }
    }
}
