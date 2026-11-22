using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace PCManager.Services;

/// <summary>
/// Shell identity helpers.
///
/// SetCurrentProcessExplicitAppUserModelID tells the taskbar which AppUserModelID this
/// process belongs to, so it gets its own taskbar group, jump list and notification
/// identity instead of being merged with another shell process.
/// </summary>
public static class AppIdentity
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

    public static void ApplyAppUserModelId(string appId)
    {
        try
        {
            SetCurrentProcessExplicitAppUserModelID(appId);
        }
        catch (Exception)
        {
            // Older shells may not expose the export; the app still runs.
        }
    }

    /// <summary>Uses the built-in shield icon so no external .ico asset is required.</summary>
    public static void ApplyWindowIcon(Window window)
    {
        try
        {
            IntPtr handle = SystemIcons.Shield.Handle;

            if (handle != IntPtr.Zero)
            {
                window.Icon = Imaging.CreateBitmapSourceFromHIcon(
                    handle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }
        }
        catch (Exception)
        {
            // A missing icon must never prevent the window from opening.
        }
    }
}
