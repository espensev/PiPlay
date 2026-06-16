# Expand an expandable control (ComboBox) by AutomationId and screenshot the opened dropdown popup —
# for inspecting inner-elevation shadows, dropdown rounding, item rendering, etc.
#
# Scope: dev-loop change-verification ONLY (the repo Debug build), NEVER release/manual QA. The shot
# proves the popup *renders as coded* (shadow present + not clipped, rounding, item legibility); it is
# not an aesthetic sign-off (record it "renders as coded", per docs/QA_Checklist.md / the QA-recording
# convention). A soft-glass theme is what shows the ElevationPopup shadow; Sharp Dark is intentionally
# flat. Run under powershell.exe -STA (UIAutomation needs STA + the .NET-Framework host).
#
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File `
#     .claude\skills\run-piplay\scripts\capture-dropdown.ps1 -AutomationId ProfilesCombo
#
# Output token: DROPDOWN|OK|rect=<combo>|cap=<region>|<png>   or   DROPDOWN|FAIL|<reason>
param(
  [string]$AutomationId = 'ProfilesCombo',
  [int]$ProcessId = 0,
  [string]$Out = '',
  [int]$PadX = 50,        # horizontal room each side for the shadow
  [int]$PadTop = 10,      # room above the control
  [int]$Height = 230      # capture height: control + opened dropdown + bottom shadow room
)

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms
# Per-monitor DPI aware so UIAutomation BoundingRectangle (physical px) and CopyFromScreen agree.
$sig = '[DllImport("shcore.dll")] public static extern int SetProcessDpiAwareness(int v);'
try { (Add-Type -MemberDefinition $sig -Name Dpi -Namespace W -PassThru)::SetProcessDpiAwareness(2) | Out-Null } catch {}

$AE = [System.Windows.Automation.AutomationElement]
$root = $AE::RootElement
# The PiPlay main window (title "PiPlay…", NOT the "PiPlay Video Popout").
$winCond = New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::Window)
$main = $null
foreach ($w in $root.FindAll([System.Windows.Automation.TreeScope]::Children, $winCond)) {
  $n = $w.Current.Name
  if ($n -like 'PiPlay*' -and $n -notlike '*Video Popout*' -and ($ProcessId -eq 0 -or $w.Current.ProcessId -eq $ProcessId)) { $main = $w; break }
}
if (-not $main) { Write-Output 'DROPDOWN|FAIL|no PiPlay main window'; exit 1 }

$byId = New-Object System.Windows.Automation.PropertyCondition($AE::AutomationIdProperty, $AutomationId)
$ctl = $main.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $byId)
if (-not $ctl) { Write-Output "DROPDOWN|FAIL|AutomationId '$AutomationId' not found"; exit 1 }

# Raise the window (a background process can't SetForegroundWindow; minimize/restore reliably raises).
$h = [IntPtr]$main.Current.NativeWindowHandle
$u = Add-Type -MemberDefinition '[DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int n);' -Name Win -Namespace U -PassThru
$u::ShowWindow($h, 6) | Out-Null; Start-Sleep -Milliseconds 200; $u::ShowWindow($h, 9) | Out-Null; Start-Sleep -Milliseconds 400

try {
  $exp = $ctl.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
} catch { Write-Output "DROPDOWN|FAIL|'$AutomationId' is not expandable (no ExpandCollapsePattern)"; exit 1 }
$exp.Expand()
Start-Sleep -Milliseconds 700

$r = $ctl.Current.BoundingRectangle
$x = [int]($r.Left - $PadX); $y = [int]($r.Top - $PadTop); $w = [int]($r.Width + 2 * $PadX)
$bmp = New-Object System.Drawing.Bitmap($w, $Height)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($x, $y, 0, 0, (New-Object System.Drawing.Size($w, $Height)))
if (-not $Out) { $dir = Join-Path $env:TEMP 'piplay_run'; New-Item -ItemType Directory -Force -Path $dir | Out-Null; $Out = Join-Path $dir ("dropdown-$AutomationId.png") }
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Output ("DROPDOWN|OK|rect={0},{1},{2}x{3}|cap={4},{5},{6}x{7}|{8}" -f [int]$r.Left,[int]$r.Top,[int]$r.Width,[int]$r.Height,$x,$y,$w,$Height,$Out)
