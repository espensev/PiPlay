#Requires -Version 5.1
& (Join-Path $PSScriptRoot "scripts\Build-PiPlay.ps1") @args
exit $LASTEXITCODE
