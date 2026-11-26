namespace PCManager.Core;

/// <summary>How much motion the shell may use right now (plan U4).</summary>
public enum MotionLevel
{
    /// <summary>No animation at all; state changes are applied instantly.</summary>
    None = 0,

    /// <summary>Fades only. No slides, scales, springs, pulses or shakes.</summary>
    Reduced = 1,

    /// <summary>Every primitive is allowed.</summary>
    Full = 2
}

/// <summary>
/// Inputs that decide <see cref="MotionLevel"/>. Kept as a plain value so the decision is
/// unit-testable; the WPF layer fills it from the OS, the app setting and the window.
/// </summary>
public readonly record struct MotionEnvironment(
    bool SystemAnimationsEnabled,
    bool ReduceMotion,
    bool BatterySaver,
    bool RemoteSession,
    bool LowResourceMode,
    bool WindowVisible = true);

/// <summary>
/// Shared motion timings and the one place that decides whether the shell may animate.
///
/// Rules (plan U4): Windows "Animation effects" off or a hidden window means no motion;
/// the app's reduce-motion setting, battery saver, a remote session or Low Resource Mode
/// downgrade to fades only. Every primitive consults this, and nothing animates layout.
/// </summary>
public static class MotionPolicy
{
    /// <summary>Micro feedback: hover, press, icon tilt.</summary>
    public static readonly TimeSpan MicroDuration = TimeSpan.FromMilliseconds(120);

    /// <summary>Standard enter/exit for cards, rows and toasts.</summary>
    public static readonly TimeSpan StandardDuration = TimeSpan.FromMilliseconds(200);

    /// <summary>Page swap (plan range 180-260 ms).</summary>
    public static readonly TimeSpan PageDuration = TimeSpan.FromMilliseconds(200);

    /// <summary>Command palette appear.</summary>
    public static readonly TimeSpan PaletteDuration = TimeSpan.FromMilliseconds(140);

    /// <summary>Hero animations (splash, boost) must finish within 2.2 s.</summary>
    public static readonly TimeSpan HeroDuration = TimeSpan.FromMilliseconds(2200);

    /// <summary>Gap between staggered list items.</summary>
    public static readonly TimeSpan StaggerStep = TimeSpan.FromMilliseconds(30);

    /// <summary>Only the first 12 items of a list are staggered.</summary>
    public const int StaggerMaximumItems = 12;

    public static MotionLevel Resolve(in MotionEnvironment environment)
    {
        if (!environment.SystemAnimationsEnabled || !environment.WindowVisible)
        {
            return MotionLevel.None;
        }

        if (environment.ReduceMotion
            || environment.BatterySaver
            || environment.RemoteSession
            || environment.LowResourceMode)
        {
            return MotionLevel.Reduced;
        }

        return MotionLevel.Full;
    }

    public static bool AllowsFades(MotionLevel level) => level != MotionLevel.None;

    public static bool AllowsTransforms(MotionLevel level) => level == MotionLevel.Full;

    /// <summary>The duration to use, or <see cref="TimeSpan.Zero"/> when even fades are off.</summary>
    public static TimeSpan Duration(MotionLevel level, TimeSpan motion) =>
        AllowsFades(level) ? motion : TimeSpan.Zero;

    /// <summary>Delay for item <paramref name="index"/>; items past the cap share the last slot.</summary>
    public static TimeSpan StaggerDelay(int index, TimeSpan? step = null)
    {
        if (index <= 0)
        {
            return TimeSpan.Zero;
        }

        int slot = Math.Min(index, StaggerMaximumItems - 1);
        return (step ?? StaggerStep) * slot;
    }
}
