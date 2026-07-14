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
param([string]$RepoRoot = (Split-Path -Parent $PSScriptRoot))

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $RepoRoot "scripts\DeploySwap.ps1")

$sandbox = Join-Path ([System.IO.Path]::GetTempPath()) ("DeploySwapTests-" + [guid]::NewGuid().ToString("N"))
$pass = 0
$fail = 0

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
            sha256 = (Get-FileHash -LiteralPath $f.FullName -Algorithm SHA256).Hash.ToUpperInvariant()
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
}
finally {
    Write-Host ""
    if (Test-Path -LiteralPath $sandbox) {
        Remove-Item -LiteralPath $sandbox -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "--- $pass passed, $fail failed ---" -ForegroundColor $(if ($fail -gt 0) { "Red" } else { "Green" })
if ($fail -gt 0) { exit 1 }
exit 0
