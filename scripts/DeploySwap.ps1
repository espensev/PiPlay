#Requires -Version 5.1
<#
.SYNOPSIS
  Staged, verifiable, rollback-safe replacement of a deployed PiPlay copy.

.DESCRIPTION
  Dot-source this from a publish script. It exists because the deploy step used to delete the live
  payload and then copy the new one in place: any failure or interruption during that copy left the
  ONLY sanctioned manual-test installation broken, with nothing to roll back to.

  The replacement is three phases:

    1. STAGE  - copy the new payload into a sibling '<root>.staging' directory. The slow, failure-prone
                copy happens entirely outside the live copy, which is still running/intact.
    2. VERIFY - re-hash the staged payload against its own build-info.json. A bad or short copy is
                caught HERE, before the live copy is touched at all.
    3. SWAP   - move the old payload aside to a sibling '<root>.backup', then move the staged payload
                in. Both are same-volume renames (fast, near-atomic per item) rather than copies, so
                the window in which the deploy root is incomplete is as small as it can be. Any failure
                mid-swap rolls the previous payload back.

  The runtime data folder (PiPlayData) is never staged, moved, or removed - it stays in place across
  the swap, so login/session survive (ADR-0007).

  Renames are not a transaction: a hard kill (or power loss) between "old moved aside" and "new moved
  in" still leaves the deploy root incomplete. That is what Repair-InterruptedDeploy is for - the next
  publish calls it FIRST and either completes or reverses the interrupted swap before doing anything
  else, so an interrupted run can never silently degrade into a broken installation.
#>

Set-StrictMode -Version Latest

function Get-DeploySwapPaths {
    param([Parameter(Mandatory = $true)][string]$DeployRoot)

    $parent = Split-Path -Parent $DeployRoot
    if ([string]::IsNullOrWhiteSpace($parent)) {
        throw "DeployRoot must live inside a parent directory (got '$DeployRoot'); the staging/backup siblings have nowhere to go."
    }
    $leaf = Split-Path -Leaf $DeployRoot

    # Siblings, not children: they must never be seen by the deployed-artifact re-hash, and a
    # same-volume sibling keeps every swap move a rename instead of a copy.
    return [pscustomobject]@{
        DeployRoot = $DeployRoot
        Staging    = Join-Path $parent "$leaf.staging"
        Backup     = Join-Path $parent "$leaf.backup"
    }
}

<#
.SYNOPSIS
  Does the deploy root hold a payload that could actually run?
#>
function Test-DeployPayloadComplete {
    param(
        [Parameter(Mandatory = $true)][string]$DeployRoot,
        [string]$ExeName = "PiPlay.exe"
    )

    if (-not (Test-Path -LiteralPath $DeployRoot)) { return $false }
    return (Test-Path -LiteralPath (Join-Path $DeployRoot $ExeName)) -and
           (Test-Path -LiteralPath (Join-Path $DeployRoot "build-info.json"))
}

<#
.SYNOPSIS
  Complete or reverse a swap that a previous run was interrupted during. Call before every deploy.
.OUTPUTS
  $true when leftovers were found and dealt with; $false when the deploy root was already coherent.
#>
function Repair-InterruptedDeploy {
    param(
        [Parameter(Mandatory = $true)][string]$DeployRoot,
        [Parameter(Mandatory = $true)][string]$DataFolderName,
        [string]$ExeName = "PiPlay.exe"
    )

    $paths = Get-DeploySwapPaths -DeployRoot $DeployRoot
    $repaired = $false

    if (Test-Path -LiteralPath $paths.Backup) {
        if (Test-DeployPayloadComplete -DeployRoot $DeployRoot -ExeName $ExeName) {
            # The new payload did land; only the backup cleanup was lost. Keep what is deployed.
            Write-Warning "Found a leftover deploy backup from an interrupted publish; the deployed copy is complete, so the backup is being discarded."
            Remove-Item -LiteralPath $paths.Backup -Recurse -Force
        } else {
            # The old payload was moved aside and the new one never made it in: restore the old one.
            Write-Warning "Found a leftover deploy backup and an INCOMPLETE deployed copy (interrupted publish); rolling the previous copy back."
            foreach ($item in @(Get-ChildItem -LiteralPath $DeployRoot -Force -ErrorAction SilentlyContinue)) {
                if ($item.Name -ieq $DataFolderName) { continue }
                Remove-Item -LiteralPath $item.FullName -Recurse -Force
            }
            foreach ($item in @(Get-ChildItem -LiteralPath $paths.Backup -Force)) {
                Move-Item -LiteralPath $item.FullName -Destination (Join-Path $DeployRoot $item.Name) -Force
            }
            Remove-Item -LiteralPath $paths.Backup -Recurse -Force
        }
        $repaired = $true
    }

    if (Test-Path -LiteralPath $paths.Staging) {
        # Staged bytes are never authoritative: they are re-staged from the publish output every run.
        Remove-Item -LiteralPath $paths.Staging -Recurse -Force
        $repaired = $true
    }

    return $repaired
}

<#
.SYNOPSIS
  Re-hash a staged payload against its own manifest. Throws if the staged copy is not byte-clean.
.OUTPUTS
  The number of artifacts verified.
#>
function Test-StagedPayload {
    param([Parameter(Mandatory = $true)][string]$StagingDir)

    $buildInfoPath = Join-Path $StagingDir "build-info.json"
    if (-not (Test-Path -LiteralPath $buildInfoPath)) {
        throw "Staged payload has no build-info.json; refusing to swap it in."
    }

    $buildInfo = Get-Content -LiteralPath $buildInfoPath -Raw | ConvertFrom-Json
    $entries = @($buildInfo.artifactHashes)
    if ($entries.Count -eq 0) { throw "Staged payload manifest lists no artifacts; refusing to swap it in." }

    $failures = @()
    foreach ($entry in $entries) {
        $artifactPath = Join-Path $StagingDir $entry.path
        if (-not (Test-Path -LiteralPath $artifactPath)) { $failures += "missing: $($entry.path)"; continue }
        $hash = (Get-FileHash -LiteralPath $artifactPath -Algorithm SHA256).Hash.ToUpperInvariant()
        if ($hash -ne $entry.sha256) { $failures += "hash mismatch: $($entry.path)"; continue }
        if ([int64](Get-Item -LiteralPath $artifactPath).Length -ne [int64]$entry.size) {
            $failures += "size mismatch: $($entry.path)"
        }
    }

    if ($failures.Count -gt 0) {
        $preview = ($failures | Select-Object -First 8) -join "`n    "
        throw "Staged payload failed verification ($($failures.Count) artifact(s)); the deployed copy was left untouched.`n    $preview"
    }

    return $entries.Count
}

<#
.SYNOPSIS
  Move everything the backup holds back into the deploy root, merging directories.
.DESCRIPTION
  Rollback must restore what the backup ACTUALLY holds, not what we think we moved: Move-Item on a
  directory is not all-or-nothing. When a file inside it is locked, PowerShell falls back to a
  recursive copy/delete and can leave the directory HALF moved - some children already in the backup,
  the locked one still live - while still throwing. A rollback that only replayed the moves it had
  recorded as successful would skip that directory entirely and then delete the backup holding the
  only copy of those children. So: walk the backup, and merge into any directory that still exists.
#>
function Restore-DeployBackup {
    param(
        [Parameter(Mandatory = $true)][string]$BackupDir,
        [Parameter(Mandatory = $true)][string]$DeployRoot
    )

    foreach ($item in @(Get-ChildItem -LiteralPath $BackupDir -Force)) {
        $target = Join-Path $DeployRoot $item.Name
        if ($item.PSIsContainer -and (Test-Path -LiteralPath $target)) {
            # Half-moved directory: merge the backed-up children back in beside the ones left behind.
            Restore-DeployBackup -BackupDir $item.FullName -DeployRoot $target
            Remove-Item -LiteralPath $item.FullName -Recurse -Force -ErrorAction SilentlyContinue
        } else {
            Move-Item -LiteralPath $item.FullName -Destination $target -Force -ErrorAction SilentlyContinue
        }
    }
}

<#
.SYNOPSIS
  Undo a failed swap: drop the new payload, restore the previous one, and ALWAYS throw.
.DESCRIPTION
  Separated from Invoke-StagedDeploy so the harness can drive the states that matter (notably a
  destination that cannot be overwritten) without having to reproduce an exotic lock through the
  public entry point.

  The backup is deleted ONLY when it is empty - i.e. when every file in it was actually restored.
  Every step here is best-effort by necessity (a second lock, e.g. an AV scan of freshly written
  binaries, can block both the removal of a moved-in file AND the restore over it), so "the restore
  probably worked" is not something this function is allowed to assume. If anything is left in the
  backup, it is kept and its path is reported: destroying it would be the same data loss this file
  exists to prevent.
#>
function Undo-DeploySwap {
    param(
        [Parameter(Mandatory = $true)][string]$DeployRoot,
        [Parameter(Mandatory = $true)][string]$BackupDir,
        [Parameter(Mandatory = $true)][string]$StagingDir,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][string[]]$MovedIn,
        [Parameter(Mandatory = $true)][string]$SwapError,
        [string]$ExeName = "PiPlay.exe"
    )

    Write-Warning "Deploy swap failed ($SwapError); rolling the previous copy back."

    # Drop the new payload's files first so the restore lands on the shape it was taken from.
    foreach ($name in $MovedIn) {
        Remove-Item -LiteralPath (Join-Path $DeployRoot $name) -Recurse -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path -LiteralPath $BackupDir) {
        Restore-DeployBackup -BackupDir $BackupDir -DeployRoot $DeployRoot
    }
    Remove-Item -LiteralPath $StagingDir -Recurse -Force -ErrorAction SilentlyContinue

    $unrestored = @(Get-ChildItem -LiteralPath $BackupDir -Recurse -Force -File -ErrorAction SilentlyContinue)
    if ($unrestored.Count -gt 0) {
        throw "Stable deploy failed mid-swap AND the rollback could not fully restore the previous copy ($($unrestored.Count) file(s) could not be put back). The previous payload is PRESERVED at '$BackupDir' - restore it by hand or re-run the publish. Do NOT test from $DeployRoot. Original failure: $SwapError"
    }
    Remove-Item -LiteralPath $BackupDir -Recurse -Force -ErrorAction SilentlyContinue

    if (-not (Test-DeployPayloadComplete -DeployRoot $DeployRoot -ExeName $ExeName)) {
        throw "Stable deploy failed mid-swap AND the rollback could not restore a runnable copy at $DeployRoot. Re-run the publish before testing anything from it. Original failure: $SwapError"
    }
    throw "Stable deploy failed mid-swap and the previous copy was rolled back: $SwapError"
}

<#
.SYNOPSIS
  Stage, verify, and swap a new payload into the deploy root, preserving the runtime data folder.
.DESCRIPTION
  Throws (after rolling the previous payload back) rather than leaving a broken deploy root.
#>
function Invoke-StagedDeploy {
    param(
        [Parameter(Mandatory = $true)][string]$DeployRoot,
        [Parameter(Mandatory = $true)][string]$SourceDir,        # the publish output to deploy (…\bin\publish\latest)
        [Parameter(Mandatory = $true)][string]$DataFolderName,   # preserved in place across the swap
        [Parameter(Mandatory = $true)][string]$MarkerName,
        [Parameter(Mandatory = $true)][string]$MarkerText,
        [string]$ExeName = "PiPlay.exe"
    )

    if (-not (Test-Path -LiteralPath $SourceDir)) { throw "Publish output not found at $SourceDir." }
    $paths = Get-DeploySwapPaths -DeployRoot $DeployRoot
    New-Item -ItemType Directory -Path $DeployRoot -Force | Out-Null

    # 1. STAGE - the slow copy happens beside the live copy, never over it.
    if (Test-Path -LiteralPath $paths.Staging) { Remove-Item -LiteralPath $paths.Staging -Recurse -Force }
    New-Item -ItemType Directory -Path $paths.Staging -Force | Out-Null
    Copy-Item -Path (Join-Path $SourceDir "*") -Destination $paths.Staging -Recurse -Force

    # The marker ships WITH the payload, so a rollback restores the old marker alongside the old bytes
    # and the two can never disagree about what is deployed.
    Set-Content -LiteralPath (Join-Path $paths.Staging $MarkerName) -Value $MarkerText -Encoding UTF8

    # 2. VERIFY - a corrupt/short copy dies here, with the live copy still intact and running.
    $verifiedCount = Test-StagedPayload -StagingDir $paths.Staging
    if (-not (Test-DeployPayloadComplete -DeployRoot $paths.Staging -ExeName $ExeName)) {
        Remove-Item -LiteralPath $paths.Staging -Recurse -Force -ErrorAction SilentlyContinue
        throw "Staged payload is missing $ExeName or build-info.json; refusing to swap it in."
    }
    Write-Host "  Staged payload verified: $verifiedCount artifact(s) re-hashed clean." -ForegroundColor DarkGray

    # 3. SWAP - renames only.
    if (Test-Path -LiteralPath $paths.Backup) { Remove-Item -LiteralPath $paths.Backup -Recurse -Force }
    New-Item -ItemType Directory -Path $paths.Backup -Force | Out-Null

    $movedIn = New-Object System.Collections.Generic.List[string]
    try {
        foreach ($item in @(Get-ChildItem -LiteralPath $DeployRoot -Force)) {
            if ($item.Name -ieq $DataFolderName) { continue }   # runtime data stays put (ADR-0007)
            Move-Item -LiteralPath $item.FullName -Destination (Join-Path $paths.Backup $item.Name) -Force
        }
        foreach ($item in @(Get-ChildItem -LiteralPath $paths.Staging -Force)) {
            Move-Item -LiteralPath $item.FullName -Destination (Join-Path $DeployRoot $item.Name) -Force
            $movedIn.Add($item.Name)
        }
    } catch {
        Undo-DeploySwap -DeployRoot $DeployRoot -BackupDir $paths.Backup -StagingDir $paths.Staging `
            -MovedIn $movedIn.ToArray() -SwapError $_.Exception.Message -ExeName $ExeName
    }

    Remove-Item -LiteralPath $paths.Staging -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $paths.Backup -Recurse -Force -ErrorAction SilentlyContinue

    return $verifiedCount
}
