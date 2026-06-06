<#
.SYNOPSIS
  Passively screenshot every visible top-level PiPlay window as the user currently sees it —
  no ShowWindow / SetForegroundWindow, so it does NOT steal focus. Use when PiPlay is already
  running (e.g. the user is mid-use) and you just want to document the current state.

.NOTES
  Run under Windows PowerShell 5.1 with STA (UIAutomation needs both):
    powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File <this>
#>
[CmdletBinding()]
param(
    [string]$ProcessName = "PiPlay",
    [string]$OutDir = (Join-Path $env:TEMP "piplay_run")
)
$ErrorActionPreference = "Stop"

$procs = Get-Process $ProcessName -ErrorAction SilentlyContinue
if (-not $procs) { Write-Output "PROC|NONE|no '$ProcessName' process running"; exit 1 }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class W2 {
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("shcore.dll")] public static extern int SetProcessDpiAwareness(int v);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int s);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@
try { [void][W2]::SetProcessDpiAwareness(2) } catch {}
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

function Get-Rect([IntPtr]$h) {
    $r = New-Object W2+RECT
    if ([W2]::DwmGetWindowAttribute($h, 9, [ref]$r, 16) -ne 0) { [void][W2]::GetWindowRect($h, [ref]$r) }
    return $r
}

$AE = [System.Windows.Automation.AutomationElement]
$idProp = [System.Windows.Automation.AutomationElement]::ProcessIdProperty
$n = 0
foreach ($proc in $procs) {
    $pc = New-Object System.Windows.Automation.PropertyCondition($idProp, $proc.Id)
    $wins = $AE::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children, $pc)
    foreach ($win in $wins) {
        $wh = [IntPtr]$win.Current.NativeWindowHandle
        if ($wh -eq [IntPtr]::Zero -or -not [W2]::IsWindowVisible($wh) -or [W2]::IsIconic($wh)) { continue }
        $r = Get-Rect $wh; $w = $r.Right - $r.Left; $ht = $r.Bottom - $r.Top
        if ($w -le 0 -or $ht -le 0) { continue }
        $bmp = New-Object System.Drawing.Bitmap($w, $ht); $g = [System.Drawing.Graphics]::FromImage($bmp)
        try { $g.CopyFromScreen($r.Left, $r.Top, 0, 0, (New-Object System.Drawing.Size($w, $ht))) } catch {}
        $cp = $bmp.GetPixel([int]($w/2), [int]($ht/2)); $path = Join-Path $OutDir "live-win$n.png"
        $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose()
        Write-Output "WIN|$n|name=$($win.Current.Name)|${w}x${ht}|center=$($cp.R),$($cp.G),$($cp.B)|$path"
        $n++
    }
}
if ($n -eq 0) { Write-Output "WIN|NONE|no visible top-level window" }
Write-Output "DONE"
