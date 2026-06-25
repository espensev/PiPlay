#requires -Version 7
<#
.SYNOPSIS
  Manual end-to-end UI smoke for PiPlay: launches the built exe, asserts key UI elements via
  UI Automation, and captures a screenshot for the spec section 22.2 chrome acceptance review.
.DESCRIPTION
  Layer 4 of the regression suite (see docs/superpowers/plans/2026-05-31-regression-test-suite.md). NOT part of
  `dotnet test` — it needs an interactive desktop, the WebView2 runtime, and network. Run it as
  a release gate alongside docs/QA_Checklist.md section 8. Capture at a fractional DPI (e.g.
  150%) to expose the rounding/clipping class of bug (see docs/AGENTS.md).
.EXAMPLE
  pwsh -File scripts/Test-UiSmoke.ps1
.EXAMPLE
  pwsh -File scripts/Test-UiSmoke.ps1 -ExePath bin\publish\latest\PiPlay.exe
#>
param(
    [string]$ExePath = "$PSScriptRoot\..\bin\publish\latest\PiPlay.exe",
    [string]$EvidenceDir = "$PSScriptRoot\..\docs\evidence",
    [int]$ReadyTimeoutSec = 30
)

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
$ErrorActionPreference = 'Stop'

if (-not (Test-Path $ExePath)) {
    throw "PiPlay.exe not found at '$ExePath'. Build a publish first: .\Build-PiPlay.ps1 -Stage Publish"
}
New-Item -ItemType Directory -Force -Path $EvidenceDir | Out-Null

$proc = Start-Process -FilePath $ExePath -PassThru
try {
    $deadline = (Get-Date).AddSeconds($ReadyTimeoutSec)
    $root = $null
    while ((Get-Date) -lt $deadline -and -not $root) {
        Start-Sleep -Milliseconds 400
        $root = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
            [System.Windows.Automation.TreeScope]::Children,
            (New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)))
    }
    if (-not $root) { throw "PiPlay main window did not appear within $ReadyTimeoutSec s." }

    function Assert-Element([string]$automationId, [string]$label) {
        $cond = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $automationId)
        $el = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
        if (-not $el) { throw "MISSING UI element: $label (AutomationId=$automationId)" }
        Write-Host "OK  $label" -ForegroundColor Green
    }

    # WPF maps x:Name -> AutomationId, so these match the named controls in MainWindow.xaml.
    Assert-Element 'PopOutButton'  'Pop out video button'
    Assert-Element 'UrlBox'        'URL / address box'
    Assert-Element 'CloseButton'   'Close caption button'
    Assert-Element 'ProfilesCombo' 'Profiles dropdown'
    Assert-Element 'SettingsButton' 'Settings gear button'

    # Screenshot the window region for the chrome-acceptance review (section 22.2).
    $rect = $root.Current.BoundingRectangle
    $bmp = New-Object System.Drawing.Bitmap([int]$rect.Width, [int]$rect.Height)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen([int]$rect.X, [int]$rect.Y, 0, 0, $bmp.Size)
    $shot = Join-Path $EvidenceDir ("ui-smoke-{0}.png" -f (Get-Date -Format 'yyyyMMdd-HHmmss'))
    $bmp.Save($shot, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    Write-Host "Saved screenshot: $shot" -ForegroundColor Cyan
    Write-Host "SMOKE PASS" -ForegroundColor Green
}
finally {
    if (-not $proc.HasExited) {
        $proc.CloseMainWindow() | Out-Null
        Start-Sleep -Seconds 1
        if (-not $proc.HasExited) { $proc.Kill() }
    }
}
