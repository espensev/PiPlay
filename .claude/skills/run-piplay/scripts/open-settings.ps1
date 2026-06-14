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

# SettingsButton is briefly disabled while a privacy action is awaiting (IsEnabled flips false->true).
# Invoking it then throws ElementNotEnabledException and Settings does NOT open — report that as FAIL,
# never as a success token, so the caller doesn't capture the main window and call it "Settings open".
if (-not $btn.Current.IsEnabled) {
    Write-Output 'SETTINGS|FAIL|SettingsButton disabled (privacy action in progress?); let the page settle and retry'
    exit 1
}

try {
    $invoke = $btn.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern)
    $invoke.Invoke()
    Write-Output 'SETTINGS|INVOKED'
} catch [System.Windows.Automation.ElementNotEnabledException] {
    # Lost the race against the disable window above — the click did NOT land. Real failure.
    Write-Output 'SETTINGS|FAIL|SettingsButton disabled at invoke time; Settings did not open'
    exit 1
} catch {
    # Modal dialog holds the UIA call open; a timeout here still means the click landed.
    Write-Output "SETTINGS|INVOKED-WITH-TIMEOUT|$($_.Exception.GetType().Name)"
}
