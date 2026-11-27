using System.Runtime.InteropServices;

namespace PCManager.Services;

/// <summary>
/// Thin Win32 lookup for the monitor a window sits on and that monitor's effective DPI (W1).
/// Presentation only; the placement rules live in <c>WindowBoundsTable</c> and are unit-tested.
/// </summary>
public static class MonitorIdentity
{
    private const uint MonitorDefaultToNearest = 2;
    private const int MdtEffectiveDpi = 0;

    public static (string Device, uint Dpi) ForWindow(IntPtr window)
    {
        IntPtr monitor = MonitorFromWindow(window, MonitorDefaultToNearest);
        return Describe(monitor);
    }

    public static (string Device, uint Dpi) Primary()
    {
        IntPtr monitor = MonitorFromPoint(new PointStruct { X = 0, Y = 0 }, MonitorDefaultToNearest);
        return Describe(monitor);
    }

    /// <summary>Work area of the monitor under a window, converted to device-independent units.</summary>
    public static (System.Windows.Rect Work, string Device, uint Dpi) WorkAreaForWindow(IntPtr window)
    {
        IntPtr monitor = MonitorFromWindow(window, MonitorDefaultToNearest);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };

        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
        {
            System.Windows.Rect fallback = System.Windows.SystemParameters.WorkArea;
            return (fallback, "primary", 96);
        }

        uint dpi = 96;

        try
        {
            uint windowDpi = GetDpiForWindow(window);

            if (windowDpi > 0)
            {
                dpi = windowDpi;
            }
        }
        catch (EntryPointNotFoundException)
        {
            // Older builds: keep 96 and treat the work area as physical pixels.
        }

        double scale = dpi / 96.0;
        var work = new System.Windows.Rect(
            info.Work.Left / scale,
            info.Work.Top / scale,
            (info.Work.Right - info.Work.Left) / scale,
            (info.Work.Bottom - info.Work.Top) / scale);

        return (work, info.Device, dpi);
    }

    private static (string Device, uint Dpi) Describe(IntPtr monitor)
    {
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };

        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
        {
            return ("primary", 96);
        }

        uint dpi = 96;

        try
        {
            if (GetDpiForMonitor(monitor, MdtEffectiveDpi, out uint dpiX, out _) == 0 && dpiX > 0)
            {
                dpi = dpiX;
            }
        }
        catch (DllNotFoundException)
        {
            // Older systems: assume 100%.
        }

        return (info.Device, dpi);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PointStruct
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    {
        public int Size;
        public RectStruct Monitor;
        public RectStruct Work;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string Device;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RectStruct
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(PointStruct point, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr window);
}
