<#
.SYNOPSIS
  Launch PiPlay (Default dev build), drive the Video Popout via UIAutomation, and screenshot
  each window. Verified launch+drive recipe for the WPF + WebView2 app.

.NOTES
  Run under Windows PowerShell 5.1 with STA (UIAutomation needs both):
    powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File <this> -Url "<youtube watch url>"
  See ..\SKILL.md for the gotchas (consent wall, single-instance, DPI, CopyFromScreen).
#>
[CmdletBinding()]
param(
    [string]$Exe,
    [string]$Url = "https://www.youtube.com/watch?v=jNQXAC9IVRw",
    [string]$OutDir = (Join-Path $env:TEMP "piplay_run"),
    [switch]$Build,
    [switch]$KillExisting,
    [switch]$NoPopout
)

$ErrorActionPreference = "Stop"

# Resolve the repo root from this script's location (.claude\skills\run-piplay\scripts\ -> repo).
$repo = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..\..")).Path
if (-not $Exe) { $Exe = Join-Path $repo "src\PiPlay\bin\Debug\net10.0-windows\PiPlay.exe" }

if ($Build) {
    Write-Output "BUILD|src\PiPlay\PiPlay.csproj"
    & dotnet build (Join-Path $repo "src\PiPlay\PiPlay.csproj") -c Debug --nologo -v minimal
    if ($LASTEXITCODE -ne 0) { Write-Output "BUILD|FAIL|$LASTEXITCODE"; exit 1 }
}
if (-not (Test-Path $Exe)) { Write-Output "EXE|MISSING|$Exe (build first: dotnet build src\PiPlay\PiPlay.csproj -c Debug)"; exit 1 }
if ($KillExisting) { Get-Process PiPlay -ErrorAction SilentlyContinue | Stop-Process -Force; Start-Sleep -Milliseconds 500 }

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win {
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int n);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT p);
    [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h, uint flags);
    [DllImport("shcore.dll")] public static extern int SetProcessDpiAwareness(int v);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int s);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
}
"@
try { [void][Win]::SetProcessDpiAwareness(2) } catch {}   # PROCESS_PER_MONITOR_DPI_AWARE
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

function Get-Rect([IntPtr]$h) {
    $r = New-Object Win+RECT
    if ([Win]::DwmGetWindowAttribute($h, 9, [ref]$r, 16) -ne 0) { [void][Win]::GetWindowRect($h, [ref]$r) }  # 9 = DWMWA_EXTENDED_FRAME_BOUNDS
    return $r
}
function Capture([IntPtr]$h, [string]$path, [string]$label) {
    # SetForegroundWindow alone is blocked from a background process, so on a busy desktop the window
    # stays occluded and CopyFromScreen grabs whatever is on top. A minimize->restore cycle reliably
    # raises the window; then confirm it owns its centre point before trusting the shot.
    [void][Win]::ShowWindow($h, 6); Start-Sleep -Milliseconds 200    # SW_MINIMIZE
    [void][Win]::ShowWindow($h, 9); Start-Sleep -Milliseconds 350    # SW_RESTORE (wins foreground)
    [void][Win]::SetForegroundWindow($h); Start-Sleep -Milliseconds 300
    $r = Get-Rect $h; $w = $r.Right - $r.Left; $ht = $r.Bottom - $r.Top
    if ($w -le 0 -or $ht -le 0) { Write-Output "CAP|$label|FAIL|zero-size"; return }
    $mid = New-Object Win+POINT; $mid.X = [int]($r.Left + $w/2); $mid.Y = [int]($r.Top + $ht/2)
    $occluded = ([Win]::GetAncestor([Win]::WindowFromPoint($mid), 2) -ne $h)   # GA_ROOT = 2
    $bmp = New-Object System.Drawing.Bitmap($w, $ht); $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($r.Left, $r.Top, 0, 0, (New-Object System.Drawing.Size($w, $ht)))   # NOT PrintWindow: it returns black for WebView2
    $cp = $bmp.GetPixel([int]($w/2), [int]($ht/2)); $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    $status = if ($occluded) { "OCCLUDED" } else { "OK" }
    Write-Output "CAP|$label|$status|${w}x${ht}|center=$($cp.R),$($cp.G),$($cp.B)|$path"
}

Write-Output "LAUNCH|$Exe|$Url"
$p = Start-Process -FilePath $Exe -ArgumentList $Url -PassThru
$h = [IntPtr]::Zero
for ($i = 0; $i -lt 40; $i++) {
    Start-Sleep -Milliseconds 500; $p.Refresh()
    if ($p.HasExited) { Write-Output "PROC|EXITED|$($p.ExitCode) (single-instance hand-off? Stop-Process -Name PiPlay then retry)"; exit 1 }
    if ($p.MainWindowHandle -ne [IntPtr]::Zero) { $h = $p.MainWindowHandle; break }
}
if ($h -eq [IntPtr]::Zero) { Write-Output "MAINWIN|FAIL|no handle after 20s"; exit 2 }
Write-Output "MAINWIN|OK|handle=$h|pid=$($p.Id)"

Start-Sleep -Seconds 2;  Capture $h (Join-Path $OutDir "1-shell.png")  "shell-2s"
Start-Sleep -Seconds 8;  Capture $h (Join-Path $OutDir "2-loaded.png") "page-10s"

if (-not $NoPopout) {
    $AE = [System.Windows.Automation.AutomationElement]
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty, "PopOutButton")
    $btn = $AE::FromHandle($h).FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
    if ($null -eq $btn) { Write-Output "POPOUT|FAIL|button not found" }
    else {
        Write-Output "POPOUT|FOUND|enabled=$($btn.Current.IsEnabled)"
        if ($btn.Current.IsEnabled) {
            $btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
            Write-Output "POPOUT|INVOKED (declines with 'Open a YouTube video first' unless the page is on a watch?v= URL)"
            Start-Sleep -Seconds 5
        }
    }
}

# Capture every visible top-level window of the process (source placeholder + popout player).
$AE = [System.Windows.Automation.AutomationElement]
$pc = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $p.Id)
$wins = $AE::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children, $pc)
Write-Output "TOPWINDOWS|count=$($wins.Count)"
$n = 0
foreach ($win in $wins) {
    $wh = [IntPtr]$win.Current.NativeWindowHandle
    if ($wh -eq [IntPtr]::Zero -or -not [Win]::IsWindowVisible($wh)) { continue }
    Capture $wh (Join-Path $OutDir "3-window$n.png") "final:$($win.Current.Name)"; $n++
}
Write-Output "PID|$($p.Id)"
Write-Output "DONE|app left running - Stop-Process -Id $($p.Id) to close"
