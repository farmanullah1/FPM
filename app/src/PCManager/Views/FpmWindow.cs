using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using PCManager.Services;

namespace PCManager.Views;

/// <summary>
/// Shared base for FPM windows (W1).
///
/// It keeps the chrome behaviour in one place: real DWM rounded corners on Windows 11 instead of
/// WPF's layered-window transparency (which forces software composition and capped the UI at
/// ~30 fps), a dark system frame that follows the app theme, the Alt+Space system menu, and the
/// WM_NCHITTEST handshake that lets Windows 11 show Snap Layouts over our custom maximize button.
///
/// Windows that stay borderless on purpose (the splash and the floating toolbar) keep their own
/// class; only windows with a caption use this base.
/// </summary>
public class FpmWindow : Window
{
    private const int WmNcHitTest = 0x0084;
    private const int HtMaxButton = 9;
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwcpRound = 2;

    private HwndSource? _source;

    public FpmWindow()
    {
        SourceInitialized += OnSourceInitialized;
        Closed += OnClosed;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    /// <summary>Shows the minimize button. Fixed-size dialogs set this to false.</summary>
    public bool CanMinimize { get; set; } = true;

    /// <summary>Shows the maximize button and enables Snap Layouts.</summary>
    public bool CanMaximize { get; set; } = true;

    /// <summary>Shows the close button.</summary>
    public bool CanClose { get; set; } = true;

    /// <summary>Reserved for W2: follow the user across virtual desktops.</summary>
    public bool FollowDesktops { get; set; }

    /// <summary>
    /// The control that should behave like the system maximize button. Windows asks the window what
    /// is under the pointer; answering HTMAXBUTTON here is what makes Snap Layouts appear.
    /// </summary>
    public FrameworkElement? MaximizeHitTarget { get; set; }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _source = PresentationSource.FromVisual(this) as HwndSource;

        if (_source is not null)
        {
            _source.AddHook(WndProc);
        }

        ApplyDwmChrome();
        ThemeService.ThemeApplied += OnThemeApplied;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        ThemeService.ThemeApplied -= OnThemeApplied;

        if (_source is not null)
        {
            _source.RemoveHook(WndProc);
            _source = null;
        }
    }

    private void OnThemeApplied(bool dark) => ApplyDwmChrome(dark);

    private void ApplyDwmChrome(bool? dark = null)
    {
        IntPtr handle = new WindowInteropHelper(this).Handle;

        if (handle == IntPtr.Zero)
        {
            return;
        }

        int corner = DwmwcpRound;
        DwmSetWindowAttribute(handle, DwmwaWindowCornerPreference, ref corner, sizeof(int));

        bool useDark = dark ?? IsDarkTheme();
        int value = useDark ? 1 : 0;
        DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref value, sizeof(int));
    }

    private static bool IsDarkTheme()
    {
        if (Application.Current?.TryFindResource("App.Background") is SolidColorBrush brush)
        {
            Color color = brush.Color;
            return color.R + color.G + color.B < 384;
        }

        return false;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Custom chrome loses the default Alt+Space handling; restore the system menu.
        if (Keyboard.Modifiers == ModifierKeys.Alt && e.SystemKey == Key.Space)
        {
            e.Handled = true;
            SystemCommands.ShowSystemMenu(this, PointToScreen(new Point(12, 40)));
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmNcHitTest || !CanMaximize || MaximizeHitTarget is not { IsVisible: true } target)
        {
            return IntPtr.Zero;
        }

        long packed = lParam.ToInt64();
        int screenX = unchecked((short)(packed & 0xFFFF));
        int screenY = unchecked((short)((packed >> 16) & 0xFFFF));

        Point topLeft = target.PointToScreen(new Point(0, 0));
        var bounds = new Rect(topLeft, new Size(target.ActualWidth, target.ActualHeight));

        if (!bounds.Contains(screenX, screenY))
        {
            return IntPtr.Zero;
        }

        handled = true;
        return new IntPtr(HtMaxButton);
    }

    /// <summary>True when the desktop compositor is running (hardware WPF rendering is possible).</summary>
    public static bool IsCompositionEnabled()
    {
        try
        {
            return DwmIsCompositionEnabled(out bool enabled) == 0 && enabled;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("dwmapi.dll")]
    private static extern int DwmIsCompositionEnabled(out bool enabled);
}
