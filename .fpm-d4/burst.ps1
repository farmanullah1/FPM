param(
    [Parameter(Mandatory = $true)][string]$Prefix,
    [int]$ClickX = -1,
    [int]$ClickY = -1,
    [string]$SendKeys = '',
    [int]$Frames = 8,
    [int]$IntervalMs = 25
)

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class FpmBurst {
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, int data, UIntPtr extra);
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
'@

[FpmBurst]::SetProcessDpiAwarenessContext([IntPtr](-4)) | Out-Null

$script:best = [IntPtr]::Zero
$script:bestArea = 0
$ids = (Get-Process FPM -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
$cb = [FpmBurst+EnumProc]{
    param($h, $l)
    if (-not [FpmBurst]::IsWindowVisible($h)) { return $true }
    $owner = 0
    [FpmBurst]::GetWindowThreadProcessId($h, [ref]$owner) | Out-Null
    if ($ids -notcontains $owner) { return $true }
    $r = New-Object FpmBurst+RECT
    [FpmBurst]::GetWindowRect($h, [ref]$r) | Out-Null
    $area = ($r.Right - $r.Left) * ($r.Bottom - $r.Top)
    if ($area -gt $script:bestArea) { $script:bestArea = $area; $script:best = $h }
    return $true
}
[FpmBurst]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
$hwnd = $script:best
if ($hwnd -eq [IntPtr]::Zero) { Write-Output 'NO-WINDOW'; exit 5 }

[FpmBurst]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 500
$wr = New-Object FpmBurst+RECT
[FpmBurst]::GetWindowRect($hwnd, [ref]$wr) | Out-Null

if ($ClickX -ge 0 -and $ClickY -ge 0) {
    [FpmBurst]::SetCursorPos(($wr.Left + $ClickX), ($wr.Top + $ClickY)) | Out-Null
    Start-Sleep -Milliseconds 120
    [FpmBurst]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    [FpmBurst]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
}

if ($SendKeys -ne '') { [System.Windows.Forms.SendKeys]::SendWait($SendKeys) }

$w = $wr.Right - $wr.Left
$h = $wr.Bottom - $wr.Top
for ($i = 0; $i -lt $Frames; $i++) {
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($wr.Left, $wr.Top, 0, 0, $bmp.Size)
    $bmp.Save(($Prefix + $i + '.png'), [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    Start-Sleep -Milliseconds $IntervalMs
}
Write-Output "burst $Prefix 0..$($Frames-1)"
