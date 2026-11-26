namespace PCManager.Core;

/// <summary>
/// Splash-screen timing rules (plan U5): visible at least 600 ms so it never flashes,
/// held no longer than 3 s after the shell is ready, and skipped entirely on request.
/// </summary>
public static class SplashTiming
{
    public static readonly TimeSpan MinimumVisible = TimeSpan.FromMilliseconds(600);

    public static readonly TimeSpan MaximumVisible = TimeSpan.FromMilliseconds(3000);

    /// <summary>How long to keep the splash up after the shell is ready.</summary>
    public static TimeSpan DelayFor(TimeSpan elapsed)
    {
        if (elapsed >= MaximumVisible || elapsed >= MinimumVisible)
        {
            return TimeSpan.Zero;
        }

        return MinimumVisible - elapsed;
    }

    /// <summary>True once the splash has been visible too long and must close.</summary>
    public static bool HasReachedLimit(TimeSpan elapsed) => elapsed >= MaximumVisible;
}
