#requires -Version 7
<#
.SYNOPSIS
  Manual end-to-end UI smoke for PiPlay: launches the built exe, asserts key UI elements via
  UI Automation, and captures a screenshot for the final deployed smoke.
.DESCRIPTION
  Final deployed-window smoke (see docs/QA_Checklist.md). NOT part of `dotnet test` — it needs an
  interactive desktop, the WebView2 runtime, and network. It checks the five named Source controls
  and captures the rendered window from an isolated data root; real playback/audio acceptance
  remains an end-user check. The capture is per-monitor-DPI aware, foregrounds the actual PiPlay
  HWND, and rejects blank/uniform frames instead of reporting a false pass.
.EXAMPLE
  pwsh -File scripts/Test-UiSmoke.ps1
.EXAMPLE
  pwsh -File scripts/Test-UiSmoke.ps1 -ExePath bin\publish\latest\PiPlay.exe
#>
param(
    [string]$ExePath = "$PSScriptRoot\..\bin\publish\latest\PiPlay.exe",
    [string]$EvidenceDir = "$PSScriptRoot\..\docs\evidence",
    [string]$DataRoot,
    [int]$ReadyTimeoutSec = 30
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $ExePath)) {
    throw "PiPlay.exe not found at '$ExePath'. Build a publish first: .\Build-PiPlay.ps1 -Stage Publish"
}
New-Item -ItemType Directory -Force -Path $EvidenceDir | Out-Null

$ownsDataRoot = [string]::IsNullOrWhiteSpace($DataRoot)
if ($ownsDataRoot) {
    $DataRoot = Join-Path ([IO.Path]::GetTempPath()) ("PiPlayUiSmokeData-" + [Guid]::NewGuid().ToString("N"))
}
New-Item -ItemType Directory -Force -Path $DataRoot | Out-Null

Add-Type @'
using System;
using System.Runtime.InteropServices;

public static class PiPlayUiSmokeNative
{
    [DllImport("user32.dll")]
    public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int command);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}
'@

# UI Automation reports physical pixels. CopyFromScreen must run in the same per-monitor-v2
# coordinate space or a window on a scaled secondary monitor can capture an unrelated/black region.
$previousDpiContext = [PiPlayUiSmokeNative]::SetThreadDpiAwarenessContext([IntPtr](-4))
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing

$previousDataRoot = [Environment]::GetEnvironmentVariable('PIPLAY_DATA_ROOT', 'Process')
$proc = $null
try {
    [Environment]::SetEnvironmentVariable('PIPLAY_DATA_ROOT', $DataRoot, 'Process')
    try {
        $proc = Start-Process -FilePath $ExePath -PassThru
    }
    finally {
        [Environment]::SetEnvironmentVariable('PIPLAY_DATA_ROOT', $previousDataRoot, 'Process')
    }

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

    $windowHandle = [IntPtr]$root.Current.NativeWindowHandle
    if ($windowHandle -eq [IntPtr]::Zero) { throw 'PiPlay main window has no native HWND.' }
    [void][PiPlayUiSmokeNative]::ShowWindow($windowHandle, 9) # SW_RESTORE
    [void][PiPlayUiSmokeNative]::SetForegroundWindow($windowHandle)
    $root.SetFocus()
    Start-Sleep -Milliseconds 750
    $foregroundProcessId = [uint32]0
    [void][PiPlayUiSmokeNative]::GetWindowThreadProcessId(
        [PiPlayUiSmokeNative]::GetForegroundWindow(), [ref]$foregroundProcessId)
    if ($foregroundProcessId -ne [uint32]$proc.Id) {
        throw 'PiPlay main window could not be foregrounded for a trustworthy rendered capture.'
    }

    # Screenshot the window region for the chrome-acceptance review (section 22.2).
    $rect = $root.Current.BoundingRectangle
    if ($rect.Width -lt 1 -or $rect.Height -lt 1) { throw 'PiPlay main window has an empty capture rectangle.' }
    $bmp = New-Object System.Drawing.Bitmap([int]$rect.Width, [int]$rect.Height)
    try {
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        try {
            $g.CopyFromScreen([int]$rect.X, [int]$rect.Y, 0, 0, $bmp.Size)
        }
        finally {
            $g.Dispose()
        }

        $colors = [System.Collections.Generic.HashSet[int]]::new()
        $stepX = [Math]::Max(1, [int]($bmp.Width / 24))
        $stepY = [Math]::Max(1, [int]($bmp.Height / 16))
        for ($y = 0; $y -lt $bmp.Height; $y += $stepY) {
            for ($x = 0; $x -lt $bmp.Width; $x += $stepX) {
                [void]$colors.Add($bmp.GetPixel($x, $y).ToArgb() -band 0x00FFFFFF)
            }
        }
        if ($colors.Count -lt 4) {
            throw "Rendered capture is blank or uniform ($($colors.Count) sampled color(s)); refusing a false smoke pass."
        }

        $shot = Join-Path $EvidenceDir ("ui-smoke-{0}.png" -f (Get-Date -Format 'yyyyMMdd-HHmmss'))
        $bmp.Save($shot, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $bmp.Dispose()
    }
    Write-Host "Saved screenshot: $shot" -ForegroundColor Cyan
    Write-Host "SMOKE PASS" -ForegroundColor Green
}
finally {
    if ($null -ne $proc -and -not $proc.HasExited) {
        $proc.CloseMainWindow() | Out-Null
        Start-Sleep -Seconds 1
        if (-not $proc.HasExited) { $proc.Kill() }
    }
    if ($previousDpiContext -ne [IntPtr]::Zero) {
        [void][PiPlayUiSmokeNative]::SetThreadDpiAwarenessContext($previousDpiContext)
    }
    if ($ownsDataRoot -and (Test-Path -LiteralPath $DataRoot)) {
        for ($attempt = 1; $attempt -le 5; $attempt++) {
            try {
                Remove-Item -LiteralPath $DataRoot -Recurse -Force -ErrorAction Stop
                break
            }
            catch {
                if ($attempt -eq 5) {
                    Write-Warning "Could not remove isolated UI-smoke data at '$DataRoot': $($_.Exception.Message)"
                }
                else {
                    Start-Sleep -Milliseconds 400
                }
            }
        }
    }
}
