using System.Runtime.InteropServices;

namespace PCManager.Services.Interop;

/// <summary>
/// Every P/Invoke used by the app lives here so the native surface stays auditable.
/// </summary>
internal static class NativeMethods
{
    // ---------------------------------------------------------------------
    // kernel32: physical memory
    // ---------------------------------------------------------------------

    /// <summary>Mirror of the Win32 MEMORYSTATUSEX structure.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    /// <summary>
    /// Fills MEMORYSTATUSEX with physical/commit memory figures. dwLength must be set
    /// to sizeof(MEMORYSTATUSEX) before the call or it fails with ERROR_INVALID_PARAMETER.
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    // ---------------------------------------------------------------------
    // kernel32: process working-set trimming
    // ---------------------------------------------------------------------

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr OpenProcess(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseHandle(IntPtr hObject);

    /// <summary>
    /// Passing (SIZE_T)-1 for both sizes tells Windows to trim the working set down to
    /// the minimum it can. Requires PROCESS_SET_QUOTA and PROCESS_QUERY_INFORMATION.
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetProcessWorkingSetSizeEx(IntPtr hProcess, IntPtr dwMinimumWorkingSetSize, IntPtr dwMaximumWorkingSetSize, uint flags);

    internal const uint PROCESS_QUERY_INFORMATION = 0x0400;
    internal const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    internal const uint PROCESS_SET_QUOTA = 0x0100;

    // ---------------------------------------------------------------------
    // shell32: recycle bin
    // ---------------------------------------------------------------------

    [StructLayout(LayoutKind.Sequential)]
    internal struct SHQUERYRBINFO
    {
        public int cbSize;
        public long i64Size;
        public long i64NumItems;
    }

    /// <summary>Returns S_OK (0) and the total size/item count of the recycle bin.</summary>
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    internal static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

    /// <summary>Empties the recycle bin. Flags below suppress the confirmation dialog and sound.</summary>
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    internal static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    internal const uint SHERB_NOCONFIRMATION = 0x00000001;
    internal const uint SHERB_NOPROGRESSUI = 0x00000002;
    internal const uint SHERB_NOSOUND = 0x00000004;

    // ---------------------------------------------------------------------
    // user32: virtual screen bounds (multi-monitor aware)
    // ---------------------------------------------------------------------

    [DllImport("user32.dll")]
    internal static extern int GetSystemMetrics(int nIndex);

    internal const int SM_XVIRTUALSCREEN = 76;
    internal const int SM_YVIRTUALSCREEN = 77;
    internal const int SM_CXVIRTUALSCREEN = 78;
    internal const int SM_CYVIRTUALSCREEN = 79;
}
