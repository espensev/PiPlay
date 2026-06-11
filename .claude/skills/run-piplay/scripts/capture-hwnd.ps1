# Capture one specific top-level window by HWND (DWM extended bounds + CopyFromScreen,
# per the skill's screenshot recipe). Raises it first so the shot isn't the occluder.
param(
    [Parameter(Mandatory = $true)][long]$Hwnd,
    [string]$OutFile = "$env:TEMP\piplay_run\hwnd.png"
)

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class HwndCap {
    public struct RECT { public int L, T, R, B; }
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int s);
    [DllImport("shcore.dll")] public static extern int SetProcessDpiAwareness(int v);
}
"@

[void][HwndCap]::SetProcessDpiAwareness(2)
$h = [IntPtr]$Hwnd
[void][HwndCap]::SetForegroundWindow($h)
Start-Sleep -Milliseconds 500

$r = New-Object HwndCap+RECT
if ([HwndCap]::DwmGetWindowAttribute($h, 9, [ref]$r, 16) -ne 0) {
    Write-Output "CAP|FAIL|DwmGetWindowAttribute"
    exit 1
}
$wd = $r.R - $r.L
$ht = $r.B - $r.T
$bmp = New-Object System.Drawing.Bitmap($wd, $ht)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.L, $r.T, 0, 0, (New-Object System.Drawing.Size($wd, $ht)))
$null = New-Item -ItemType Directory -Force (Split-Path $OutFile)
$bmp.Save($OutFile)
Write-Output "CAP|OK|${wd}x${ht}|$OutFile"
