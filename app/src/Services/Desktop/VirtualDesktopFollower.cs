using PCManager.Services.Abstractions;

namespace PCManager.Services.Desktop;

/// <summary>
/// Keeps an owned window on whichever virtual desktop the user is viewing (W2).
///
/// Windows has no documented "pin to all desktops" API, so this follows the documented route:
/// when the window sits on another desktop, move it to the desktop of the foreground window. The
/// COM move does not activate the window, so focus, z-order and topmost are left alone. The UI owns
/// the 500 ms loop (<c>DesktopFollowLoop</c>) so this class stays free of WPF and testable.
/// </summary>
public sealed class VirtualDesktopFollower : IDisposable
{
    private readonly IVirtualDesktopManagerApi _api;
    private readonly Func<IntPtr> _windowHandle;
    private readonly Func<bool> _shouldFollow;
    private readonly IAppLogger _logger;
    private DateTime _lastFailureLog = DateTime.MinValue;

    public VirtualDesktopFollower(
        IVirtualDesktopManagerApi api,
        Func<IntPtr> windowHandle,
        Func<bool> shouldFollow,
        IAppLogger logger)
    {
        _api = api;
        _windowHandle = windowHandle;
        _shouldFollow = shouldFollow;
        _logger = logger;
    }

    /// <summary>One poll. Returns true when the window was moved to another desktop.</summary>
    public bool TickOnce()
    {
        if (!_shouldFollow())
        {
            return false;
        }

        IntPtr window = _windowHandle();

        if (window == IntPtr.Zero || _api.IsOnCurrentDesktop(window))
        {
            return false;
        }

        // The foreground window belongs to the desktop the user is looking at. When it is one of ours
        // the window is already current, so a failed lookup means "leave it alone", never "guess".
        if (!_api.TryGetDesktopId(_api.ForegroundWindow, out Guid desktop) || desktop == Guid.Empty)
        {
            LogFailureOnce();
            return false;
        }

        if (!_api.MoveToDesktop(window, desktop))
        {
            LogFailureOnce();
            return false;
        }

        _logger.Info($"Virtual desktop: moved window to {desktop}.");
        return true;
    }

    public void Dispose()
    {
        if (_api is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private void LogFailureOnce()
    {
        DateTime now = DateTime.UtcNow;

        if (now - _lastFailureLog < TimeSpan.FromMinutes(1))
        {
            return;
        }

        _lastFailureLog = now;
        _logger.Info("Virtual desktop: window is on another desktop but its id is unavailable; left in place.");
    }
}
