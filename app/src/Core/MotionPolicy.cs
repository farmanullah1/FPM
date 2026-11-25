namespace PCManager.Core;

/// <summary>
/// Shared motion timings, and the one place that decides whether the shell may animate.
/// Every animation asks <see cref="ShouldAnimate"/> first, so switching off Windows'
/// "Animation effects" makes the app change instantly instead of animating.
/// </summary>
public static class MotionPolicy
{
    /// <summary>Fade-and-lift used when a page is swapped in.</summary>
    public static readonly TimeSpan PageDuration = TimeSpan.FromMilliseconds(180);

    /// <summary>Shorter fade-and-zoom used when the command palette appears.</summary>
    public static readonly TimeSpan PaletteDuration = TimeSpan.FromMilliseconds(140);

    /// <summary>True when the user has left Windows animations enabled.</summary>
    public static bool ShouldAnimate(bool systemAllowsAnimations) => systemAllowsAnimations;

    /// <summary>The duration to use, or <see cref="TimeSpan.Zero"/> when motion is off.</summary>
    public static TimeSpan Duration(bool systemAllowsAnimations, TimeSpan motion)
        => systemAllowsAnimations ? motion : TimeSpan.Zero;
}
