#Requires -Version 5.1

function Invoke-NativeCommandQuiet {
    param([Parameter(Mandatory = $true)][scriptblock]$Command)

    $previousErrorActionPreference = $ErrorActionPreference
    try {
        # Windows PowerShell can promote native stderr to NativeCommandError under EAP=Stop even
        # when stderr is redirected. Callers still use the native exit code as the truth source.
        $ErrorActionPreference = "Continue"
        & $Command 2>$null
    } finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
}
