# Open the Settings dialog on the running PiPlay instance via UIAutomation (STA, PS 5.1).
# The dialog is modal (ShowDialog), so Invoke() may time out while the dialog stays open —
# that's the success case; swallow the timeout and let the caller capture.
param([int]$ProcessId = 0)

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

$root = [Windows.Automation.AutomationElement]::RootElement
$cond = New-Object Windows.Automation.PropertyCondition(
    [Windows.Automation.AutomationElement]::ClassNameProperty, 'Window')
$windows = $root.FindAll([Windows.Automation.TreeScope]::Children, $cond)

$main = $null
foreach ($w in $windows) {
    if ($ProcessId -ne 0 -and $w.Current.ProcessId -ne $ProcessId) { continue }
    if ($w.Current.Name -like 'PiPlay*' -and $w.Current.Name -notlike '*Popout*') { $main = $w; break }
}
if (-not $main) { Write-Output 'SETTINGS|FAIL|main window not found'; exit 1 }

$btnCond = New-Object Windows.Automation.PropertyCondition(
    [Windows.Automation.AutomationElement]::AutomationIdProperty, 'SettingsButton')
$btn = $main.FindFirst([Windows.Automation.TreeScope]::Descendants, $btnCond)
if (-not $btn) { Write-Output 'SETTINGS|FAIL|SettingsButton not found'; exit 1 }

try {
    $invoke = $btn.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern)
    $invoke.Invoke()
    Write-Output 'SETTINGS|INVOKED'
} catch {
    # Modal dialog holds the UIA call open; a timeout here still means the click landed.
    Write-Output "SETTINGS|INVOKED-WITH-TIMEOUT|$($_.Exception.GetType().Name)"
}
