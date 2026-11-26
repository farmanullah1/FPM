$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class U6 {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr hWnd, int x, int y, int w, int h, bool repaint);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}
"@

[U6]::SetProcessDPIAware() | Out-Null
$root = Split-Path -Parent $PSScriptRoot
$env:PCMANAGER_FRAME_TRACE = '1'
$proc = Start-Process -FilePath (Join-Path $root 'app/src/PCManager/bin/Debug/net8.0-windows/FPM.exe') -PassThru

$handle = [IntPtr]::Zero
for ($i = 0; $i -lt 40 -and $handle -eq [IntPtr]::Zero; $i++) {
    Start-Sleep -Seconds 1
    $proc.Refresh()
    if ($proc.HasExited) { throw "FPM exited with $($proc.ExitCode)" }
    $handle = $proc.MainWindowHandle
}
if ($handle -eq [IntPtr]::Zero) { throw 'Main window not found' }

[U6]::MoveWindow($handle, 20, 20, 700, 1125, $true) | Out-Null
[U6]::SetForegroundWindow($handle) | Out-Null
Start-Sleep -Seconds 4

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
    $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Seconds 2
}

function Capture([string]$name) {
    $rect = New-Object U6+RECT
    [U6]::GetWindowRect($handle, [ref]$rect) | Out-Null
    $bitmap = New-Object System.Drawing.Bitmap ($rect.Right - $rect.Left), ($rect.Bottom - $rect.Top)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $hdc = $graphics.GetHdc()
    [U6]::PrintWindow($handle, $hdc, 2) | Out-Null
    $graphics.ReleaseHdc($hdc)
    $graphics.Dispose()
    $bitmap.Save((Join-Path $root $name), [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
    Write-Output $name
}

SelectNav 'Memory'
SelectNav 'Home'
SelectNav 'Memory'
Capture '.u6-nav.png'

# Scroll Memory's relief list, leave the page, come back: the offset must be preserved.
$scrollCondition = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::IsScrollPatternAvailableProperty, $true)
$scrollables = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $scrollCondition)
$pattern = $null
foreach ($element in $scrollables) {
    $candidate = $element.GetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern)
    if ($candidate.Current.VerticallyScrollable -and $element.Current.BoundingRectangle.Height -gt 500) {
        $pattern = $candidate
        break
    }
}
if ($null -ne $pattern) {
    try {
        $pattern.SetScrollPercent([System.Windows.Automation.ScrollPattern]::NoScroll, 70)
        Start-Sleep -Milliseconds 400
        Write-Output ("scrolled to {0}" -f $pattern.Current.VerticalScrollPercent)
    } catch {
        Write-Output "scroll skipped: $($_.Exception.Message)"
    }
} else {
    Write-Output 'no vertically scrollable pane found'
}

SelectNav 'Home'
SelectNav 'Memory'
Capture '.u6-scroll.png'

SelectNav 'Settings'
Start-Sleep -Seconds 1
Capture '.u6-settings.png'

$proc.CloseMainWindow() | Out-Null
Start-Sleep -Seconds 3
if (-not $proc.HasExited) { $proc.Kill() }
Write-Output 'done'
