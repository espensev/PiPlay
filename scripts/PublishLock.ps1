#Requires -Version 5.1
<#
.SYNOPSIS
  Cross-process publish lock. Dot-source from a publish script.

.DESCRIPTION
  Two concurrent publishes would interleave on the repo's bin\publish tree, on the deploy root
  mid-swap, and on stable tag creation. These take a named mutex per protected resource and fail fast
  rather than corrupting either side.

  The subtlety that makes Close-PublishLocks mandatory: a Windows mutex is owned by the THREAD that
  took it, and PowerShell's console host reuses its pipeline thread across commands. A script that
  simply ended without releasing would leave the mutex owned by a thread that is still alive in the
  terminal, so the NEXT publish - from any other process - would be told "another publish is already
  running" when nothing is, and would only start working again if and when the GC got round to
  finalizing the handle. Safety was never at risk (two publishes still could not interleave); liveness
  was, and nondeterministically. So the caller MUST release on every exit path:

      . (Join-Path $PSScriptRoot "PublishLock.ps1")
      New-PublishLock -Key "repo|$repoRoot" -What "this repository" | Out-Null
      try   { <the publish> }
      finally { Close-PublishLocks }

  A publish that CRASHES needs no cleanup: process death closes the handle, and the next run's
  WaitOne throws AbandonedMutexException, which transfers ownership - so a killed publish leaves no
  stale lock either.
#>

Set-StrictMode -Version Latest

$script:publishLocks = @()

function New-PublishLock {
    param(
        [Parameter(Mandatory = $true)][string]$Key,
        [Parameter(Mandatory = $true)][string]$What
    )

    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($Key.ToLowerInvariant()))
    } finally {
        $sha.Dispose()
    }
    $name = "Local\PiPlayPublish-" + (([System.BitConverter]::ToString($bytes) -replace '-', '').Substring(0, 32))

    $mutex = New-Object System.Threading.Mutex($false, $name)
    $acquired = $false
    try {
        $acquired = $mutex.WaitOne(0)
    } catch [System.Threading.AbandonedMutexException] {
        $acquired = $true   # the previous owner died holding it; ownership transfers to us
    }
    if (-not $acquired) {
        $mutex.Dispose()
        throw "Another PiPlay publish is already running against $What. Wait for it to finish (or stop it) and re-run."
    }

    $script:publishLocks += $mutex
    return $mutex
}

<#
.SYNOPSIS
  Release every lock this script took. Call from a finally - see the note in the file header.
#>
function Close-PublishLocks {
    foreach ($mutex in $script:publishLocks) {
        try { $mutex.ReleaseMutex() } catch { <# not held (e.g. abandoned) - nothing to release #> }
        try { $mutex.Dispose() } catch { <# best effort #> }
    }
    $script:publishLocks = @()
}
