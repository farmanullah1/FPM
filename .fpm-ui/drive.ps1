param(
    [Parameter(Mandatory = $true)][string]$OutPath,
    [string]$SendKeys = '',
    [int]$WidthDip = 1200
)

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class FpmUi {
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
'@

[FpmUi]::SetProcessDpiAwarenessContext([IntPtr](-4)) | Out-Null
$proc = Get-Process FPM -ErrorAction Stop | Select-Object -First 1
$hwnd = $proc.MainWindowHandle
$dpi = [FpmUi]::GetDpiForWindow($hwnd)

if ($WidthDip -gt 0) {
    $px = [int]($WidthDip * $dpi / 96)
    [FpmUi]::SetWindowPos($hwnd, [IntPtr](-2), 40, 40, $px, 900, 0x0010 -bor 0x0040) | Out-Null
}

[FpmUi]::SetWindowPos($hwnd, [IntPtr](-1), 0, 0, 0, 0, 0x0003) | Out-Null
[FpmUi]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 700

if ($SendKeys -ne '') {
    [System.Windows.Forms.SendKeys]::SendWait($SendKeys)
    Start-Sleep -Milliseconds 900
}

$r = New-Object FpmUi+RECT
[FpmUi]::GetWindowRect($hwnd, [ref]$r) | Out-Null
$w = $r.Right - $r.Left
$ht = $r.Bottom - $r.Top
$bmp = New-Object System.Drawing.Bitmap $w, $ht
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.Left, $r.Top, 0, 0, $bmp.Size)
$bmp.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose()
$bmp.Dispose()
[FpmUi]::SetWindowPos($hwnd, [IntPtr](-2), 0, 0, 0, 0, 0x0003) | Out-Null
Write-Output "SAVED $OutPath ${w}x${ht}"
