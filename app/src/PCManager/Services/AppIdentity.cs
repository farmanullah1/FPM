using System.Runtime.InteropServices;
using System.Windows;
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

    /// <summary>Applies the embedded original FPM icon to a secondary window.</summary>
    public static void ApplyWindowIcon(Window window)
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/fpm.ico", UriKind.Absolute);
            window.Icon = BitmapFrame.Create(uri, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        }
        catch (Exception)
        {
            // A missing icon must never prevent a window from opening.
        }
    }
}
