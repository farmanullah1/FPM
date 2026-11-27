using System.Windows.Threading;
using PCManager.Services.Abstractions;
using PCManager.Services.Desktop;

namespace PCManager.Services;

/// <summary>
/// Drives <see cref="VirtualDesktopFollower"/> on the UI thread (W2). One loop per followed window;
/// it stops itself when the window is hidden or minimised, so idle windows cost nothing.
/// </summary>
public sealed class DesktopFollowLoop : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    private readonly VirtualDesktopFollower? _follower;
    private DispatcherTimer? _timer;

    private DesktopFollowLoop(VirtualDesktopFollower? follower)
    {
        _follower = follower;
    }

    public static DesktopFollowLoop Start(
        Func<IntPtr> windowHandle,
        Func<bool> shouldFollow,
        IAppLogger logger)
    {
        if (!VirtualDesktopManagerApi.TryCreate(out IVirtualDesktopManagerApi? api) || api is null)
        {
            logger.Info("Virtual desktop API unavailable; desktop following is off.");
            return new DesktopFollowLoop(null);
        }

        var loop = new DesktopFollowLoop(
            new VirtualDesktopFollower(api, windowHandle, shouldFollow, logger));
        loop._timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = PollInterval };
        loop._timer.Tick += (_, _) => loop._follower!.TickOnce();
        loop._timer.Start();
        logger.Info("Virtual desktop tracking started (500 ms, visible windows only).");
        return loop;
    }

    public void Dispose()
    {
        _timer?.Stop();
        _timer = null;
        _follower?.Dispose();
    }
}
