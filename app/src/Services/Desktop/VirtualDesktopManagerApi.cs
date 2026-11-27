using System.Runtime.InteropServices;

namespace PCManager.Services.Desktop;

/// <summary>
/// The documented Windows virtual-desktop API (W2). Only windows owned by our process can be moved,
/// which is exactly the contract the follower needs.
/// </summary>
public interface IVirtualDesktopManagerApi
{
    IntPtr ForegroundWindow { get; }

    bool IsOnCurrentDesktop(IntPtr window);

    bool TryGetDesktopId(IntPtr window, out Guid desktopId);

    bool MoveToDesktop(IntPtr window, Guid desktopId);
}

/// <summary>Creates the COM <c>VirtualDesktopManager</c>; returns false where it is unavailable.</summary>
public static class VirtualDesktopManagerApi
{
    public static bool TryCreate(out IVirtualDesktopManagerApi? api)
    {
        try
        {
            api = new ComVirtualDesktopManager();
            return true;
        }
        catch (COMException)
        {
            api = null;
            return false;
        }
        catch (InvalidCastException)
        {
            api = null;
            return false;
        }
    }

    [ComImport]
    [Guid("a5cd92ff-29be-454c-8d04-d82879fb3f1b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IVirtualDesktopManager
    {
        [PreserveSig]
        int IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow, out int onCurrentDesktop);

        [PreserveSig]
        int GetWindowDesktopId(IntPtr topLevelWindow, out Guid desktopId);

        [PreserveSig]
        int MoveWindowToDesktop(IntPtr topLevelWindow, ref Guid desktopId);
    }

    private sealed class ComVirtualDesktopManager : IVirtualDesktopManagerApi, IDisposable
    {
        private readonly IVirtualDesktopManager _manager;

        public ComVirtualDesktopManager()
        {
            // CLSID_VirtualDesktopManager, activated through the shell; no undocumented interface.
            Type? classId = Type.GetTypeFromCLSID(
                new Guid("AA509086-5CA9-4C25-8F95-589D3C07B48A"), throwOnError: true);
            object instance = Activator.CreateInstance(classId!)!;
            _manager = (IVirtualDesktopManager)instance;
        }

        public IntPtr ForegroundWindow => GetForegroundWindow();

        public bool IsOnCurrentDesktop(IntPtr window) =>
            window != IntPtr.Zero &&
            _manager.IsWindowOnCurrentVirtualDesktop(window, out int onCurrent) == 0 &&
            onCurrent != 0;

        public bool TryGetDesktopId(IntPtr window, out Guid desktopId)
        {
            desktopId = Guid.Empty;
            return window != IntPtr.Zero && _manager.GetWindowDesktopId(window, out desktopId) == 0;
        }

        public bool MoveToDesktop(IntPtr window, Guid desktopId) =>
            window != IntPtr.Zero && _manager.MoveWindowToDesktop(window, ref desktopId) == 0;

        public void Dispose()
        {
            if (Marshal.IsComObject(_manager))
            {
                Marshal.ReleaseComObject(_manager);
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
    }
}
