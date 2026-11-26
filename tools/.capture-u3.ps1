$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class U3Capture {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr hWnd, int x, int y, int w, int h, bool repaint);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
"@

$root = Split-Path -Parent $PSScriptRoot
$proc = Start-Process -FilePath (Join-Path $root 'app/src/PCManager/bin/Debug/net8.0-windows/FPM.exe') -PassThru
$handle = [IntPtr]::Zero
for ($i = 0; $i -lt 40 -and $handle -eq [IntPtr]::Zero; $i++) {
    Start-Sleep -Seconds 1
    $proc.Refresh()
    if ($proc.HasExited) { throw "FPM exited with code $($proc.ExitCode)" }
    $handle = $proc.MainWindowHandle
}
if ($handle -eq [IntPtr]::Zero) { throw 'Main window not found (startup failure?)' }

[U3Capture]::MoveWindow($handle, 20, 20, 560, 900, $true) | Out-Null
[U3Capture]::SetForegroundWindow($handle) | Out-Null
Start-Sleep -Seconds 2

$window = [System.Windows.Automation.AutomationElement]::FromHandle($handle)
function SelectNav([string]$name) {
    $textCondition = New-Object System.Windows.Automation.AndCondition(
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::Text)),
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::NameProperty, $name)))

    $label = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $textCondition)
    if ($null -eq $label) { throw "nav label $name not found" }

    $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
    $item = $label
    while ($null -ne $item -and $item.Current.ControlType -ne [System.Windows.Automation.ControlType]::ListItem) {
        $item = $walker.GetParent($item)
    }
    if ($null -eq $item) { throw "nav item $name not found" }

    $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Seconds 2
}

function Capture([string]$name) {
    $rect = New-Object U3Capture+RECT
    [U3Capture]::GetWindowRect($handle, [ref]$rect) | Out-Null
    $bitmap = New-Object System.Drawing.Bitmap ($rect.Right - $rect.Left), ($rect.Bottom - $rect.Top)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $hdc = $graphics.GetHdc()
    [U3Capture]::PrintWindow($handle, $hdc, 2) | Out-Null
    $graphics.ReleaseHdc($hdc)
    $graphics.Dispose()
    $bitmap.Save((Join-Path $root $name), [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
    Write-Output $name
}

Capture '.u3-home-light.png'
SelectNav 'Protection'
Capture '.u3-protection-light.png'
SelectNav 'Memory'
Capture '.u3-memory-light.png'
SelectNav 'Storage'
Capture '.u3-storage-light.png'

SelectNav 'Settings'
$checkBox = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
    [System.Windows.Automation.ControlType]::CheckBox)
$checks = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $checkBox)
$darkToggle = $null
foreach ($check in $checks) {
    if ($check.Current.Name -like '*Dark theme*') { $darkToggle = $check; break }
}
if ($null -eq $darkToggle) { throw 'Dark theme toggle not found' }
$darkToggle.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
Start-Sleep -Seconds 2

SelectNav 'Home'
Capture '.u3-home-dark.png'
SelectNav 'Memory'
Capture '.u3-memory-dark.png'

# Restore the owner's Light theme before leaving.
SelectNav 'Settings'
$checks = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $checkBox)
foreach ($check in $checks) {
    if ($check.Current.Name -like '*Dark theme*') {
        if ($check.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Current.ToggleState -eq
            [System.Windows.Automation.ToggleState]::On) {
            $check.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
        }
        break
    }
}
Start-Sleep -Seconds 1

$proc.CloseMainWindow() | Out-Null
Start-Sleep -Seconds 3
if (-not $proc.HasExited) { $proc.Kill() }
Write-Output 'done'
