#Requires -Version 5.1
<#
.SYNOPSIS
  Behavioural harness for scripts\DeploySwap.ps1 - the staged-swap deploy used by Publish-Stable.ps1.

.DESCRIPTION
  The C# lane (ReleaseScriptPolicyTests) can only assert on script TEXT, which cannot tell you whether
  a rollback actually restores the previous copy. This exercises the real thing against a throwaway
  deploy root: a clean swap, a corrupt staged payload, a failure mid-swap (a locked file, the way a
  lingering process pins a dll), and both interrupted-publish recovery shapes.

  It caught a genuine data-loss bug on first run: Move-Item on a directory whose child is locked
  half-moves it and still throws, so a rollback keyed on "moves I recorded as successful" silently
  dropped the backed-up children before deleting the backup. Case C3 pins that.

  Deploys nothing and touches no real deploy root - everything happens under a temp sandbox.

.EXAMPLE
  .\scripts\Test-DeploySwap.ps1
#>
[CmdletBinding()]
param([string]$RepoRoot)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = Split-Path -Parent $PSScriptRoot
}

. (Join-Path $RepoRoot "scripts\DeploySwap.ps1")

$sandbox = Join-Path ([System.IO.Path]::GetTempPath()) ("DeploySwapTests-" + [guid]::NewGuid().ToString("N"))
$pass = 0
$fail = 0
$deployAliasM = $null
$sourceAliasM = $null

function Check([string]$name, [scriptblock]$assertion) {
    try {
        $result = & $assertion
        if ($result -eq $false) { throw "assertion returned false" }
        Write-Host "[ PASS ] $name" -ForegroundColor Green
        $script:pass++
    } catch {
        Write-Host "[ FAIL ] $name :: $($_.Exception.Message)" -ForegroundColor Red
        $script:fail++
    }
}

function New-Payload {
    param([string]$Dir, [string]$Token, [int]$ExtraFiles = 2)

    New-Item -ItemType Directory -Path $Dir -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $Dir "PiPlay.exe") -Value "exe-$Token" -Encoding UTF8
    New-Item -ItemType Directory -Path (Join-Path $Dir "runtimes") -Force | Out-Null
    for ($i = 0; $i -lt $ExtraFiles; $i++) {
        Set-Content -LiteralPath (Join-Path $Dir "runtimes\lib$i.dll") -Value "lib$i-$Token" -Encoding UTF8
    }

    # Manifest describing exactly these bytes (mirrors Build-PiPlay's artifactHashes shape).
    $hashes = @()
    foreach ($f in @(Get-ChildItem -LiteralPath $Dir -Recurse -File)) {
        $rel = $f.FullName.Substring($Dir.Length).TrimStart('\')
        $hashes += [pscustomobject]@{
            path   = $rel
            sha256 = Get-Sha256Hex -Path $f.FullName
            size   = [int64]$f.Length
        }
    }
    [pscustomobject]@{ version = "9.9.9"; buildNumber = 99; artifactHashes = $hashes } |
        ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $Dir "build-info.json") -Encoding UTF8
}

function New-DeployRootWithOldPayload {
    param([string]$Root)
    New-Payload -Dir $Root -Token "OLD"
    New-Item -ItemType Directory -Path (Join-Path $Root "PiPlayData") -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $Root "PiPlayData\settings.json") -Value '{"session":"precious"}' -Encoding UTF8
    Set-Content -LiteralPath (Join-Path $Root ".piplay.publish.marker") -Value "version=old" -Encoding UTF8
}

function Test-DataPreserved([string]$Root) {
    $p = Join-Path $Root "PiPlayData\settings.json"
    return (Test-Path -LiteralPath $p) -and ((Get-Content -LiteralPath $p -Raw).Trim() -eq '{"session":"precious"}')
}

function Test-NoLeftovers([string]$Root) {
    $paths = Get-DeploySwapPaths -DeployRoot $Root
    return (-not (Test-Path -LiteralPath $paths.Staging)) -and (-not (Test-Path -LiteralPath $paths.Backup))
}

function Get-ExeToken([string]$Root) {
    return (Get-Content -LiteralPath (Join-Path $Root "PiPlay.exe") -Raw).Trim()
}

Write-Host "`n--- DeploySwap behavioural harness ---" -ForegroundColor Cyan
Write-Host "Sandbox: $sandbox`n"

try {
    # ---------------------------------------------------------------- A. happy path
    $rootA = Join-Path $sandbox "A\PiPlay"
    $srcA = Join-Path $sandbox "A\src"
    New-DeployRootWithOldPayload -Root $rootA
    New-Payload -Dir $srcA -Token "NEW"

    Invoke-StagedDeploy -DeployRoot $rootA -SourceDir $srcA -DataFolderName "PiPlayData" `
        -MarkerName ".piplay.publish.marker" -MarkerText "version=new" | Out-Null

    Check "A1 new payload is live"            { (Get-ExeToken $rootA) -eq "exe-NEW" }
    Check "A2 runtime data preserved"          { Test-DataPreserved $rootA }
    Check "A3 marker replaced with new"        { (Get-Content -LiteralPath (Join-Path $rootA ".piplay.publish.marker") -Raw).Trim() -eq "version=new" }
    Check "A4 no staging/backup left behind"   { Test-NoLeftovers $rootA }
    Check "A5 nested artifacts came across"    { (Get-Content -LiteralPath (Join-Path $rootA "runtimes\lib1.dll") -Raw).Trim() -eq "lib1-NEW" }

    # ------------------------------------------------- B. corrupt staged payload aborts pre-swap
    $rootB = Join-Path $sandbox "B\PiPlay"
    $srcB = Join-Path $sandbox "B\src"
    New-DeployRootWithOldPayload -Root $rootB
    New-Payload -Dir $srcB -Token "NEW"
    # Tamper AFTER the manifest was written: the staged copy will not match its own hashes.
    Set-Content -LiteralPath (Join-Path $srcB "runtimes\lib0.dll") -Value "CORRUPTED" -Encoding UTF8

    $threwB = $false
    try {
        Invoke-StagedDeploy -DeployRoot $rootB -SourceDir $srcB -DataFolderName "PiPlayData" `
            -MarkerName ".piplay.publish.marker" -MarkerText "version=new" | Out-Null
    } catch { $threwB = $true }

    Check "B1 corrupt payload throws"                  { $threwB }
    Check "B2 live copy UNTOUCHED (still old)"         { (Get-ExeToken $rootB) -eq "exe-OLD" }
    Check "B3 old marker untouched"                    { (Get-Content -LiteralPath (Join-Path $rootB ".piplay.publish.marker") -Raw).Trim() -eq "version=old" }
    Check "B4 runtime data preserved"                  { Test-DataPreserved $rootB }

    # ------------------------------------------- C. failure mid-swap rolls the old payload back
    $rootC = Join-Path $sandbox "C\PiPlay"
    $srcC = Join-Path $sandbox "C\src"
    New-DeployRootWithOldPayload -Root $rootC
    New-Payload -Dir $srcC -Token "NEW"

    # Hold a handle open on a live file so moving it aside fails partway through the swap - the
    # realistic failure (a lingering process pinning a dll).
    $locked = [System.IO.File]::Open(
        (Join-Path $rootC "runtimes\lib1.dll"),
        [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::None)

    $threwC = $false
    try {
        Invoke-StagedDeploy -DeployRoot $rootC -SourceDir $srcC -DataFolderName "PiPlayData" `
            -MarkerName ".piplay.publish.marker" -MarkerText "version=new" | Out-Null
    } catch { $threwC = $true } finally { $locked.Dispose() }

    Check "C1 mid-swap failure throws"                 { $threwC }
    Check "C2 old exe rolled back into place"          { (Get-ExeToken $rootC) -eq "exe-OLD" }
    Check "C3 old nested artifact restored"            { (Get-Content -LiteralPath (Join-Path $rootC "runtimes\lib0.dll") -Raw).Trim() -eq "lib0-OLD" }
    Check "C4 old marker restored"                     { (Get-Content -LiteralPath (Join-Path $rootC ".piplay.publish.marker") -Raw).Trim() -eq "version=old" }
    Check "C5 runtime data preserved"                  { Test-DataPreserved $rootC }
    Check "C6 no staging/backup left behind"           { Test-NoLeftovers $rootC }
    Check "C7 deployed copy is runnable again"         { Test-DeployPayloadComplete -DeployRoot $rootC }

    # ---------- H. rollback that CANNOT restore must keep the backup, not delete it.
    # Case C is the easy direction: the swap died during move-OUT, so every restore destination was
    # already vacated and each Move-Item succeeded. The dangerous direction is a failure during
    # move-IN where a second lock (an AV scan of the freshly written binaries is the realistic one)
    # blocks BOTH the removal of the moved-in file AND the restore over it. Every rollback step is
    # -ErrorAction SilentlyContinue, so the restore fails silently - and an earlier version of this
    # code then deleted the backup anyway and reported a successful rollback, destroying the only
    # remaining copy of that artifact. Drive Undo-DeploySwap directly at exactly that state.
    $rootH = Join-Path $sandbox "H\PiPlay"
    $pathsH = Get-DeploySwapPaths -DeployRoot $rootH

    New-Payload -Dir $rootH -Token "NEW"                  # the half-swapped-in new payload
    New-Item -ItemType Directory -Path (Join-Path $rootH "PiPlayData") -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $rootH "PiPlayData\settings.json") -Value '{"session":"precious"}' -Encoding UTF8
    New-Payload -Dir $pathsH.Backup -Token "OLD"          # the previous payload, moved aside
    New-Item -ItemType Directory -Path $pathsH.Staging -Force | Out-Null

    # Hold the moved-in PiPlay.exe open with no sharing: neither the rollback's Remove-Item nor the
    # restore's Move-Item -Force can touch it.
    $lockedH = [System.IO.File]::Open(
        (Join-Path $rootH "PiPlay.exe"),
        [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::None)

    $errH = $null
    try {
        Undo-DeploySwap -DeployRoot $rootH -BackupDir $pathsH.Backup -StagingDir $pathsH.Staging `
            -MovedIn @("PiPlay.exe") -SwapError "simulated move-in failure" 3>$null
    } catch { $errH = $_.Exception.Message } finally { $lockedH.Dispose() }

    Check "H1 an unrestorable rollback throws"          { $null -ne $errH }
    Check "H2 it says the backup was PRESERVED"         { $errH -match 'PRESERVED at' }
    Check "H3 the backup still EXISTS on disk"          { Test-Path -LiteralPath $pathsH.Backup }
    Check "H4 the old exe is still recoverable from it" {
        (Get-Content -LiteralPath (Join-Path $pathsH.Backup "PiPlay.exe") -Raw).Trim() -eq "exe-OLD"
    }
    Check "H5 it does NOT claim a successful rollback"  { $errH -notmatch 'previous copy was rolled back' }
    Check "H6 runtime data preserved"                   { Test-DataPreserved $rootH }

    # --------------------- D. interrupted swap (old moved aside, new never landed) -> restore old
    $rootD = Join-Path $sandbox "D\PiPlay"
    New-DeployRootWithOldPayload -Root $rootD
    $pathsD = Get-DeploySwapPaths -DeployRoot $rootD
    # Simulate the kill window: everything but the data folder is sitting in .backup, root is bare.
    New-Item -ItemType Directory -Path $pathsD.Backup -Force | Out-Null
    foreach ($item in @(Get-ChildItem -LiteralPath $rootD -Force)) {
        if ($item.Name -ieq "PiPlayData") { continue }
        Move-Item -LiteralPath $item.FullName -Destination (Join-Path $pathsD.Backup $item.Name) -Force
    }
    New-Item -ItemType Directory -Path $pathsD.Staging -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $pathsD.Staging "PiPlay.exe") -Value "exe-HALFSTAGED" -Encoding UTF8

    Check "D0 precondition: deploy root is broken"     { -not (Test-DeployPayloadComplete -DeployRoot $rootD) }

    $repairedD = Repair-InterruptedDeploy -DeployRoot $rootD -DataFolderName "PiPlayData" 3>$null

    Check "D1 repair reports it acted"                 { $repairedD -eq $true }
    Check "D2 previous payload restored"               { (Get-ExeToken $rootD) -eq "exe-OLD" }
    Check "D3 deployed copy runnable again"            { Test-DeployPayloadComplete -DeployRoot $rootD }
    Check "D4 runtime data survived the interruption"  { Test-DataPreserved $rootD }
    Check "D5 leftovers cleared"                       { Test-NoLeftovers $rootD }

    # ------------- E. interrupted AFTER the new payload landed (only cleanup lost) -> keep the new
    $rootE = Join-Path $sandbox "E\PiPlay"
    New-Payload -Dir $rootE -Token "NEW"
    New-Item -ItemType Directory -Path (Join-Path $rootE "PiPlayData") -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $rootE "PiPlayData\settings.json") -Value '{"session":"precious"}' -Encoding UTF8
    $pathsE = Get-DeploySwapPaths -DeployRoot $rootE
    New-Payload -Dir $pathsE.Backup -Token "OLD"   # stale backup nobody removed

    $repairedE = Repair-InterruptedDeploy -DeployRoot $rootE -DataFolderName "PiPlayData" 3>$null

    Check "E1 repair reports it acted"                 { $repairedE -eq $true }
    Check "E2 the NEW payload is kept"                 { (Get-ExeToken $rootE) -eq "exe-NEW" }
    Check "E3 stale backup discarded"                  { Test-NoLeftovers $rootE }
    Check "E4 runtime data preserved"                  { Test-DataPreserved $rootE }

    # ------------------------------------------------ F. clean root: repair is a no-op
    $rootF = Join-Path $sandbox "F\PiPlay"
    New-DeployRootWithOldPayload -Root $rootF
    $repairedF = Repair-InterruptedDeploy -DeployRoot $rootF -DataFolderName "PiPlayData" 3>$null
    Check "F1 nothing to repair on a coherent root"    { $repairedF -eq $false }
    Check "F2 payload untouched"                       { (Get-ExeToken $rootF) -eq "exe-OLD" }

    # ------------------------------------------------ G. drive-root guard
    $threwG = $false
    try { Get-DeploySwapPaths -DeployRoot "E:\" | Out-Null } catch { $threwG = $true }
    Check "G1 refuses a drive root (no sibling space)" { $threwG }

    # -------------------------------- I. manifest paths cannot escape or alias the staged root
    $stageI = Join-Path $sandbox "I\staging"
    New-Item -ItemType Directory -Path $stageI -Force | Out-Null
    $outsideI = Join-Path $sandbox "I\outside.bin"
    Set-Content -LiteralPath $outsideI -Value "outside" -Encoding UTF8
    $outsideItemI = Get-Item -LiteralPath $outsideI
    $outsideEntryI = [pscustomobject]@{
        path = "..\outside.bin"
        sha256 = Get-Sha256Hex -Path $outsideI
        size = [int64]$outsideItemI.Length
    }
    [pscustomobject]@{ artifactHashes = @($outsideEntryI) } |
        ConvertTo-Json -Depth 5 |
        Set-Content -LiteralPath (Join-Path $stageI "build-info.json") -Encoding UTF8

    $errI1 = $null
    try { Test-StagedPayload -StagingDir $stageI | Out-Null } catch { $errI1 = $_.Exception.Message }
    Check "I1 staged verification rejects parent traversal" { $errI1 -match 'traversal' }

    $insideI = Join-Path $stageI "PiPlay.exe"
    Set-Content -LiteralPath $insideI -Value "inside" -Encoding UTF8
    $insideItemI = Get-Item -LiteralPath $insideI
    $insideEntryI = [pscustomobject]@{
        path = "PiPlay.exe"
        sha256 = Get-Sha256Hex -Path $insideI
        size = [int64]$insideItemI.Length
    }
    [pscustomobject]@{ artifactHashes = @($insideEntryI, $insideEntryI) } |
        ConvertTo-Json -Depth 5 |
        Set-Content -LiteralPath (Join-Path $stageI "build-info.json") -Encoding UTF8

    $errI2 = $null
    try { Test-StagedPayload -StagingDir $stageI | Out-Null } catch { $errI2 = $_.Exception.Message }
    Check "I2 staged verification rejects duplicate artifacts" { $errI2 -match 'duplicate' }

    # ------------------ J. a publish source nested in the deploy root is rejected before mutation
    $rootJ = Join-Path $sandbox "J\PiPlay"
    $srcJ = Join-Path $rootJ "source"
    New-DeployRootWithOldPayload -Root $rootJ
    New-Payload -Dir $srcJ -Token "NEW"
    $errJ = $null
    try {
        Invoke-StagedDeploy -DeployRoot $rootJ -SourceDir $srcJ -DataFolderName "PiPlayData" `
            -MarkerName ".piplay.publish.marker" -MarkerText "version=new" | Out-Null
    } catch { $errJ = $_.Exception.Message }
    Check "J1 nested source/deploy overlap fails closed" { $errJ -match 'overlap' }
    Check "J2 overlap guard preserves the old deploy"   { (Get-ExeToken $rootJ) -eq "exe-OLD" }
    Check "J3 overlap guard preserves the source"       { (Get-Content -LiteralPath (Join-Path $srcJ "PiPlay.exe") -Raw).Trim() -eq "exe-NEW" }

    # ------------------------ K. a source at the staging sibling is not deleted during pre-cleanup
    $rootK = Join-Path $sandbox "K\PiPlay"
    New-DeployRootWithOldPayload -Root $rootK
    $pathsK = Get-DeploySwapPaths -DeployRoot $rootK
    New-Payload -Dir $pathsK.Staging -Token "NEW"
    $errK = $null
    try {
        Invoke-StagedDeploy -DeployRoot $rootK -SourceDir $pathsK.Staging -DataFolderName "PiPlayData" `
            -MarkerName ".piplay.publish.marker" -MarkerText "version=new" | Out-Null
    } catch { $errK = $_.Exception.Message }
    Check "K1 staging/source overlap fails closed" { $errK -match 'overlap' }
    Check "K2 staging/source guard preserves source" {
        (Get-Content -LiteralPath (Join-Path $pathsK.Staging "PiPlay.exe") -Raw).Trim() -eq "exe-NEW"
    }

    # -------------------------------------- L. swap-path derivation rejects the active repository
    $errL = $null
    try { Get-DeploySwapPaths -DeployRoot $RepoRoot | Out-Null } catch { $errL = $_.Exception.Message }
    Check "L1 repository deploy root fails closed" { $errL -match 'overlap.*repository' }

    # -------- M. independent aliases to one physical payload must fail before the marker is changed
    $physicalM = Join-Path $sandbox "M\physical"
    $aliasesM = Join-Path $sandbox "M\aliases"
    New-Payload -Dir $physicalM -Token "OLD"
    Set-Content -LiteralPath (Join-Path $physicalM ".piplay.publish.marker") `
        -Value "version=old" -Encoding UTF8
    New-Item -ItemType Directory -Path $aliasesM -Force | Out-Null
    $deployAliasM = Join-Path $aliasesM "deploy"
    $sourceAliasM = Join-Path $aliasesM "source"
    New-Item -ItemType Junction -Path $deployAliasM -Target $physicalM | Out-Null
    New-Item -ItemType Junction -Path $sourceAliasM -Target $physicalM | Out-Null

    $errM = $null
    try {
        Invoke-StagedDeploy -DeployRoot $deployAliasM -SourceDir $sourceAliasM `
            -DataFolderName "PiPlayData" -MarkerName ".piplay.publish.marker" `
            -MarkerText "version=new" | Out-Null
    } catch { $errM = $_.Exception.Message }
    Check "M1 physical source/deploy aliases fail closed" { $errM -match 'reparse point' }
    Check "M2 alias rejection occurs before marker mutation" {
        (Get-Content -LiteralPath (Join-Path $physicalM ".piplay.publish.marker") -Raw).Trim() -eq "version=old"
    }

    # ---------------- N. an extended namespace alias is rejected before touching the real payload
    $rootN = Join-Path $sandbox "N\PiPlay"
    $srcN = Join-Path $sandbox "N\source"
    New-DeployRootWithOldPayload -Root $rootN
    New-Payload -Dir $srcN -Token "NEW"
    $namespaceRootN = "\\?\$rootN"
    $errN = $null
    try {
        Invoke-StagedDeploy -DeployRoot $namespaceRootN -SourceDir $srcN `
            -DataFolderName "PiPlayData" -MarkerName ".piplay.publish.marker" `
            -MarkerText "version=new" | Out-Null
    } catch { $errN = $_.Exception.Message }
    Check "N1 namespace deploy alias fails closed" { $errN -match 'Windows device or extended path namespace' }
    Check "N2 namespace rejection preserves old executable" { (Get-ExeToken $rootN) -eq "exe-OLD" }
    Check "N3 namespace rejection preserves old marker" {
        (Get-Content -LiteralPath (Join-Path $rootN ".piplay.publish.marker") -Raw).Trim() -eq "version=old"
    }
}
finally {
    Write-Host ""
    foreach ($alias in @($deployAliasM, $sourceAliasM)) {
        if ($alias -and (Test-Path -LiteralPath $alias)) {
            [System.IO.Directory]::Delete($alias)
        }
    }
    if (Test-Path -LiteralPath $sandbox) {
        Remove-Item -LiteralPath $sandbox -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "--- $pass passed, $fail failed ---" -ForegroundColor $(if ($fail -gt 0) { "Red" } else { "Green" })
if ($fail -gt 0) { exit 1 }
exit 0
