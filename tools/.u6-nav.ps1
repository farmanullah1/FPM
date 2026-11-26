$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class U6N {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}
"@
[U6N]::SetProcessDPIAware() | Out-Null

$root = Split-Path -Parent $PSScriptRoot
$proc = Start-Process -FilePath (Join-Path $root 'app/src/PCManager/bin/Debug/net8.0-windows/FPM.exe') -PassThru
$handle = [IntPtr]::Zero
for ($i = 0; $i -lt 40 -and $handle -eq [IntPtr]::Zero; $i++) {
    Start-Sleep -Seconds 1
    $proc.Refresh()
    $handle = $proc.MainWindowHandle
}
Start-Sleep -Seconds 3
$window = [System.Windows.Automation.AutomationElement]::FromHandle($handle)

$items = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::ListItem)))
foreach ($item in $items) {
    $b = $item.Current.BoundingRectangle
    Write-Output ("item '{0}' at {1},{2} {3}x{4}" -f $item.Current.Name, [int]$b.X, [int]$b.Y, [int]$b.Width, [int]$b.Height)
}

$rect = New-Object U6N+RECT
[U6N]::GetWindowRect($handle, [ref]$rect) | Out-Null
$bitmap = New-Object System.Drawing.Bitmap ($rect.Right - $rect.Left), ($rect.Bottom - $rect.Top)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$hdc = $graphics.GetHdc()
[U6N]::PrintWindow($handle, $hdc, 2) | Out-Null
$graphics.ReleaseHdc($hdc)
$graphics.Dispose()
$bitmap.Save((Join-Path $root '.u6-default.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$bitmap.Dispose()

$proc.CloseMainWindow() | Out-Null
Start-Sleep -Seconds 3
if (-not $proc.HasExited) { $proc.Kill() }
Write-Output 'done'
