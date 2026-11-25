param(
    [Parameter(Mandatory = $true)][string]$OutPath,
    [int]$WidthDip = 0,
    [int]$HeightDip = 0,
    [int]$ClickX = -1,
    [int]$ClickY = -1,
    [string]$SendKeys = '',
    [int]$WaitAfter = 1100
)

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class FpmD4 {
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
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

[FpmD4]::SetProcessDpiAwarenessContext([IntPtr](-4)) | Out-Null

$script:best = [IntPtr]::Zero
$script:bestArea = 0
$callback = [FpmD4+EnumProc]{
    param($h, $l)
    if (-not [FpmD4]::IsWindowVisible($h)) { return $true }
    $pidOwner = 0
    [FpmD4]::GetWindowThreadProcessId($h, [ref]$pidOwner) | Out-Null
    $procIds = (Get-Process FPM -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
    if ($procIds -notcontains $pidOwner) { return $true }
    $r = New-Object FpmD4+RECT
    [FpmD4]::GetWindowRect($h, [ref]$r) | Out-Null
    $area = ($r.Right - $r.Left) * ($r.Bottom - $r.Top)
    if ($area -gt $script:bestArea) { $script:bestArea = $area; $script:best = $h }
    return $true
}

$hwnd = [IntPtr]::Zero
for ($wait = 0; $wait -lt 40; $wait++) {
    $script:best = [IntPtr]::Zero
    $script:bestArea = 0
    [FpmD4]::EnumWindows($callback, [IntPtr]::Zero) | Out-Null
    if ($script:bestArea -gt 200000) { $hwnd = $script:best; break }
    Start-Sleep -Milliseconds 500
}

if ($hwnd -eq [IntPtr]::Zero) { Write-Output 'WINDOW-NOT-READY'; exit 5 }
$dpi = [FpmD4]::GetDpiForWindow($hwnd)

if ($WidthDip -gt 0) {
    $px = [int]($WidthDip * $dpi / 96)
    $ht = [int]($HeightDip * $dpi / 96)
    [FpmD4]::SetWindowPos($hwnd, [IntPtr](-2), 10, 10, $px, $ht, 0x0010 -bor 0x0040) | Out-Null
}

[FpmD4]::SetWindowPos($hwnd, [IntPtr](-1), 0, 0, 0, 0, 0x0003) | Out-Null
[FpmD4]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 800

$wr = New-Object FpmD4+RECT
[FpmD4]::GetWindowRect($hwnd, [ref]$wr) | Out-Null

if ($ClickX -ge 0 -and $ClickY -ge 0) {
    [FpmD4]::SetCursorPos(($wr.Left + $ClickX), ($wr.Top + $ClickY)) | Out-Null
    Start-Sleep -Milliseconds 200
    [FpmD4]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    [FpmD4]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds $WaitAfter
}

if ($SendKeys -ne '') {
    [System.Windows.Forms.SendKeys]::SendWait($SendKeys)
    Start-Sleep -Milliseconds $WaitAfter
}

$r = New-Object FpmD4+RECT
[FpmD4]::GetWindowRect($hwnd, [ref]$r) | Out-Null
$w = $r.Right - $r.Left
$ht = $r.Bottom - $r.Top
$bmp = New-Object System.Drawing.Bitmap $w, $ht
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.Left, $r.Top, 0, 0, $bmp.Size)
$bmp.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose()
$bmp.Dispose()
[FpmD4]::SetWindowPos($hwnd, [IntPtr](-2), 0, 0, 0, 0, 0x0003) | Out-Null
Write-Output "SAVED $OutPath ${w}x${ht} (dpi=$dpi)"
