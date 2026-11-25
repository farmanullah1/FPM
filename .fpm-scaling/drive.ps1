param(
    [Parameter(Mandatory = $true)][string]$OutPath,
    [int]$WidthDip = 0,
    [int]$HeightDip = 0,
    [string]$SendKeys = '',
    [int]$ClickX = -1,
    [int]$ClickY = -1,
    [int]$HoverX = -1,
    [int]$HoverY = -1,
    [switch]$Maximize
)

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class FpmScale {
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint data, UIntPtr extra);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
'@

[FpmScale]::SetProcessDpiAwarenessContext([IntPtr](-4)) | Out-Null

$hwnd = [IntPtr]::Zero
for ($wait = 0; $wait -lt 40; $wait++) {
    $proc = Get-Process FPM -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($proc -ne $null -and $proc.MainWindowHandle -ne [IntPtr]::Zero) {
        $probe = New-Object FpmScale+RECT
        [FpmScale]::GetWindowRect($proc.MainWindowHandle, [ref]$probe) | Out-Null
        if (($probe.Right - $probe.Left) -gt 100) { $hwnd = $proc.MainWindowHandle; break }
    }
    Start-Sleep -Milliseconds 500
}

if ($hwnd -eq [IntPtr]::Zero) { Write-Output 'WINDOW-NOT-READY'; exit 5 }
$dpi = [FpmScale]::GetDpiForWindow($hwnd)

if ($Maximize) {
    $root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
    $idCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'MaximizeButton')
    $maxButton = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $idCondition)
    $screenWidth = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds.Width
    for ($attempt = 0; $attempt -lt 2; $attempt++) {
        $probe = New-Object FpmScale+RECT
        [FpmScale]::GetWindowRect($hwnd, [ref]$probe) | Out-Null
        if (($probe.Right - $probe.Left) -ge ($screenWidth * 0.95)) { break }
        $maxButton.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        Start-Sleep -Milliseconds 800
    }
}

if ($WidthDip -gt 0) {
    $px = [int]($WidthDip * $dpi / 96)
    $ht = [int]($HeightDip * $dpi / 96)
    [FpmScale]::SetWindowPos($hwnd, [IntPtr](-2), 10, 10, $px, $ht, 0x0010 -bor 0x0040) | Out-Null
}

[FpmScale]::SetWindowPos($hwnd, [IntPtr](-1), 0, 0, 0, 0, 0x0003) | Out-Null
[FpmScale]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 800

if ($HoverX -ge 0 -and $HoverY -ge 0) {
    $hr = New-Object FpmScale+RECT
    [FpmScale]::GetWindowRect($hwnd, [ref]$hr) | Out-Null
    [FpmScale]::SetCursorPos(($hr.Left + $HoverX), ($hr.Top + $HoverY)) | Out-Null
    Start-Sleep -Milliseconds 2300
}

if ($ClickX -ge 0 -and $ClickY -ge 0) {
    $r = New-Object FpmScale+RECT
    [FpmScale]::GetWindowRect($hwnd, [ref]$r) | Out-Null
    [FpmScale]::SetCursorPos(($r.Left + $ClickX), ($r.Top + $ClickY)) | Out-Null
    Start-Sleep -Milliseconds 250
    [FpmScale]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    [FpmScale]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 1200
}

if ($SendKeys -ne '') {
    [System.Windows.Forms.SendKeys]::SendWait($SendKeys)
    Start-Sleep -Milliseconds 900
}

$r = New-Object FpmScale+RECT
[FpmScale]::GetWindowRect($hwnd, [ref]$r) | Out-Null
$w = $r.Right - $r.Left
$ht = $r.Bottom - $r.Top
$bmp = New-Object System.Drawing.Bitmap $w, $ht
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.Left, $r.Top, 0, 0, $bmp.Size)
$bmp.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose()
$bmp.Dispose()
[FpmScale]::SetWindowPos($hwnd, [IntPtr](-2), 0, 0, 0, 0, 0x0003) | Out-Null
Write-Output "SAVED $OutPath ${w}x${ht} (dpi=$dpi)"
