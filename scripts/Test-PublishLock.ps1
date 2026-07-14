#Requires -Version 5.1
<#
.SYNOPSIS
  Behavioural harness for scripts\PublishLock.ps1 — the cross-process publish lock.

.DESCRIPTION
  The C# lane can only assert that the lock's TEXT exists, which says nothing about whether it locks,
  or (the bug this exists to catch) whether it ever lets go.

  A Windows mutex is owned by the THREAD that took it, and PowerShell's console host reuses its
  pipeline thread across commands. An earlier version of the lock never released: after a publish
  finished in an interactive session, the still-alive prompt thread kept OWNING the mutex, so the next
  publish from any other process was told "another publish is already running" when nothing was — and
  un-blocked itself only if and when the GC finalized the handle. Case 3 pins that.

  Uses a unique key per run, so it never touches a real publish lock.

.EXAMPLE
  .\scripts\Test-PublishLock.ps1
#>
[CmdletBinding()]
param([string]$RepoRoot = (Split-Path -Parent $PSScriptRoot))

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $RepoRoot "scripts\PublishLock.ps1")

$key = "test|" + [guid]::NewGuid().ToString("N")
$pass = 0
$fail = 0

function Check([string]$name, [scriptblock]$assertion) {
    try {
        if ((& $assertion) -eq $false) { throw "assertion returned false" }
        Write-Host "[ PASS ] $name" -ForegroundColor Green
        $script:pass++
    } catch {
        Write-Host "[ FAIL ] $name :: $($_.Exception.Message)" -ForegroundColor Red
        $script:fail++
    }
}

# Ask a SEPARATE pwsh process whether it can take the lock. This is the only honest way to test a
# cross-process lock: same-thread reentrancy would let an in-process probe succeed either way.
function Test-OtherProcessCanAcquire {
    $probe = @"
Set-StrictMode -Version Latest
`$ErrorActionPreference = 'Stop'
. '$(Join-Path $RepoRoot "scripts\PublishLock.ps1")'
try {
    New-PublishLock -Key '$key' -What 'the harness lock' | Out-Null
    Close-PublishLocks
    'ACQUIRED'
} catch {
    'BLOCKED'
}
"@
    $out = & pwsh -NoProfile -NonInteractive -Command $probe 2>&1
    return (($out | Out-String).Trim() -match 'ACQUIRED')
}

Write-Host "`n--- PublishLock behavioural harness ---" -ForegroundColor Cyan
Write-Host "Key: $key`n"

try {
    # 1. Baseline: nothing holds the lock.
    Check "1 an unheld lock is acquirable by another process" { Test-OtherProcessCanAcquire }

    # 2. While WE hold it, another process must be refused.
    New-PublishLock -Key $key -What "the harness lock" | Out-Null
    Check "2 a held lock blocks another process" { -not (Test-OtherProcessCanAcquire) }

    # 3. THE REGRESSION: once we release, the lock must be free IMMEDIATELY - not whenever the GC
    #    happens to finalize the handle. This is the phantom-lock bug; before Close-PublishLocks
    #    existed, this returned BLOCKED with nothing running.
    Close-PublishLocks
    Check "3 releasing frees it immediately (no phantom lock)" { Test-OtherProcessCanAcquire }

    # 4. Releasing twice must not throw (the finally can run after an early failure).
    Check "4 Close-PublishLocks is idempotent" {
        Close-PublishLocks
        Close-PublishLocks
        $true
    }

    # 5. A second, sequential publish in the SAME session must work - the common case after a publish
    #    fails on a tag collision and you re-run it.
    New-PublishLock -Key $key -What "the harness lock" | Out-Null
    Close-PublishLocks
    Check "5 sequential re-acquire in the same session works" { Test-OtherProcessCanAcquire }

    # 6. Distinct keys are independent locks (the repo lock must not block an unrelated deploy root).
    New-PublishLock -Key "$key|repo" -What "repo" | Out-Null
    Check "6 a different key is not blocked" { Test-OtherProcessCanAcquire }
    Close-PublishLocks

    # 7. Same-thread reentrancy: a mutex is reentrant for its owner, so a nested acquire succeeds and
    #    bumps the recursion count. Each acquire is recorded, so each gets its own ReleaseMutex - if it
    #    did not, the count would never reach zero and the lock would stay held after Close.
    New-PublishLock -Key $key -What "the harness lock" | Out-Null
    $threw = $false
    try { New-PublishLock -Key $key -What "the harness lock" | Out-Null } catch { $threw = $true }
    Check "7 a nested same-thread acquire does not throw" { -not $threw }

    Close-PublishLocks
    Check "8 a nested acquire is fully unwound by Close"  { Test-OtherProcessCanAcquire }
}
finally {
    Close-PublishLocks
}

Write-Host ""
Write-Host "--- $pass passed, $fail failed ---" -ForegroundColor $(if ($fail -gt 0) { "Red" } else { "Green" })
if ($fail -gt 0) { exit 1 }
exit 0
