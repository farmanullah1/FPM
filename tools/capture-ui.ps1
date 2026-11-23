param([string]$Out = "ui-home.png", [int]$X = -1, [int]$Y = -1)

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public class W {
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);
  [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
}
'@

# PER_MONITOR_AWARE_V2 so the capture uses real pixels, not virtualized ones.
[void][W]::SetProcessDpiAwarenessContext([IntPtr](-4))

$screen = [System.Windows.Forms.Screen]::PrimaryScreen
"bounds=$($screen.Bounds) work=$($screen.WorkingArea)"

$proc = Get-Process FarmanullahPCManager -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { throw "FarmanullahPCManager is not running" }
$h = $proc.MainWindowHandle

"dpi=$([W]::GetDpiForWindow($h))"
$r = New-Object W+RECT
[void][W]::GetWindowRect($h, [ref]$r)
"before=$($r.L),$($r.T) size=$($r.R - $r.L)x$($r.B - $r.T)"

if ($X -ge 0) {
    # SWP_NOSIZE | SWP_NOZORDER
    [void][W]::SetWindowPos($h, [IntPtr]::Zero, $X, $Y, 0, 0, 0x0001 -bor 0x0004)
    Start-Sleep -Milliseconds 400
}

# Bring it above the host window for an unobstructed capture.
# SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW
[void][W]::SetWindowPos($h, [IntPtr](-1), 0, 0, 0, 0, 0x0002 -bor 0x0001 -bor 0x0040)
Start-Sleep -Milliseconds 700

[void][W]::GetWindowRect($h, [ref]$r)
$w = $r.R - $r.L
$ht = $r.B - $r.T
"after=$($r.L),$($r.T) size=${w}x${ht}"

$bmp = New-Object System.Drawing.Bitmap $w, $ht
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.L, $r.T, 0, 0, $bmp.Size)
$bmp.Save((Join-Path (Get-Location) $Out), [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose()
$bmp.Dispose()

# Back to normal z-order.
[void][W]::SetWindowPos($h, [IntPtr](-2), 0, 0, 0, 0, 0x0002 -bor 0x0001)
"saved $Out"
