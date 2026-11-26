using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using PCManager.Core;

namespace PCManager.Motion;

/// <summary>Direction a slide enters from.</summary>
public enum SlideEdge
{
    Left,
    Right,
    Top,
    Bottom
}

/// <summary>
/// The U4 motion kit. Every primitive asks <see cref="Policy"/> for the current level first:
/// Full animates, Reduced keeps fades only, None applies the final state instantly.
///
/// Only RenderTransform and Opacity are animated; geometry, layout and brushes are never
/// animated, so no primitive can trigger a layout pass.
/// </summary>
public static partial class Motion
{
    private static IMotionPolicy _policy = new SystemMotionPolicy();

    /// <summary>The active policy. App startup swaps in the settings-aware implementation.</summary>
    public static IMotionPolicy Policy => _policy;

    public static MotionLevel Level => _policy.Level;

    public static void UsePolicy(IMotionPolicy policy) => _policy = policy;

    /// <summary>Fade from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static void Fade(UIElement element, double from, double to, TimeSpan? duration = null)
    {
        MotionLevel level = Level;

        if (!MotionPolicy.AllowsFades(level))
        {
            element.BeginAnimation(UIElement.OpacityProperty, null);
            element.Opacity = to;
            return;
        }

        element.BeginAnimation(
            UIElement.OpacityProperty,
            new DoubleAnimation(from, to, duration ?? MotionPolicy.StandardDuration)
            {
                EasingFunction = EnterEase(),
                FillBehavior = FillBehavior.HoldEnd
            });
    }

    public static void FadeIn(UIElement element, TimeSpan? duration = null, double from = 0d) =>
        Fade(element, from, 1d, duration);

    public static void FadeOut(UIElement element, TimeSpan? duration = null) =>
        Fade(element, 1d, 0d, duration);

    /// <summary>Slide a transform into place; reduced/off just resets it.</summary>
    public static void SlideIn(
        TranslateTransform transform,
        double fromX,
        double fromY,
        TimeSpan? duration = null)
    {
        if (!MotionPolicy.AllowsTransforms(Level))
        {
            transform.BeginAnimation(TranslateTransform.XProperty, null);
            transform.BeginAnimation(TranslateTransform.YProperty, null);
            transform.X = 0;
            transform.Y = 0;
            return;
        }

        TimeSpan motion = duration ?? MotionPolicy.StandardDuration;
        IEasingFunction easing = EnterEase();

        transform.BeginAnimation(
            TranslateTransform.XProperty,
            new DoubleAnimation(fromX, 0d, motion) { EasingFunction = easing });
        transform.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(fromY, 0d, motion) { EasingFunction = easing });
    }

    /// <summary>Slide a named edge into place around an element's own transform.</summary>
    public static void SlideIn(FrameworkElement element, SlideEdge edge, double distance = 12d, TimeSpan? duration = null)
    {
        TranslateTransform transform = Translate(element);
        (double x, double y) = edge switch
        {
            SlideEdge.Left => (-distance, 0d),
            SlideEdge.Right => (distance, 0d),
            SlideEdge.Top => (0d, -distance),
            _ => (0d, distance)
        };

        SlideIn(transform, x, y, duration);
    }

    /// <summary>Zoom an element in from <paramref name="from"/> about its centre.</summary>
    public static void ScaleIn(FrameworkElement element, double from = 0.96d, TimeSpan? duration = null)
    {
        ScaleIn(EnsureScale(element), from, duration);
    }

    public static void ScaleIn(ScaleTransform transform, double from, TimeSpan? duration = null)
    {
        if (!MotionPolicy.AllowsTransforms(Level))
        {
            ResetScale(transform);
            return;
        }

        TimeSpan motion = duration ?? MotionPolicy.StandardDuration;
        IEasingFunction easing = EnterEase();

        transform.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            new DoubleAnimation(from, 1d, motion) { EasingFunction = easing });
        transform.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            new DoubleAnimation(from, 1d, motion) { EasingFunction = easing });
    }

    /// <summary>Springy scale for toggles and the logo. Full motion only.</summary>
    public static void Spring(FrameworkElement element, double from = 0.86d)
    {
        if (!MotionPolicy.AllowsTransforms(Level))
        {
            ResetScale(FindScale(element));
            return;
        }

        ScaleTransform transform = EnsureScale(element);
        var spring = new ElasticEase
        {
            EasingMode = EasingMode.EaseOut,
            Oscillations = 2,
            Springiness = 5
        };

        transform.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            new DoubleAnimation(from, 1d, MotionPolicy.StandardDuration) { EasingFunction = spring });
        transform.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            new DoubleAnimation(from, 1d, MotionPolicy.StandardDuration) { EasingFunction = spring });
    }

    /// <summary>Stagger entry for a list; only the first 12 items get their own delay.</summary>
    public static void Stagger(IEnumerable<FrameworkElement> items, TimeSpan? step = null, int maxItems = MotionPolicy.StaggerMaximumItems)
    {
        int index = 0;

        foreach (FrameworkElement item in items)
        {
            if (index >= maxItems)
            {
                break;
            }

            TimeSpan delay = MotionPolicy.AllowsTransforms(Level)
                ? MotionPolicy.StaggerDelay(index, step)
                : TimeSpan.Zero;

            item.BeginAnimation(
                UIElement.OpacityProperty,
                new DoubleAnimation(0d, 1d, MotionPolicy.StandardDuration)
                {
                    BeginTime = delay,
                    EasingFunction = EnterEase(),
                    FillBehavior = FillBehavior.HoldEnd
                });

            index++;
        }
    }

    /// <summary>Error feedback: a short horizontal shake. Full motion only.</summary>
    public static void Shake(FrameworkElement element)
    {
        if (!MotionPolicy.AllowsTransforms(Level))
        {
            return;
        }

        TranslateTransform transform = Translate(element);
        var shake = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(240) };

        foreach ((double value, int ms) in new[] { (-4d, 40), (4d, 80), (-3d, 120), (3d, 160), (0d, 200) })
        {
            shake.KeyFrames.Add(new LinearDoubleKeyFrame(value, TimeSpan.FromMilliseconds(ms)));
        }

        transform.BeginAnimation(TranslateTransform.XProperty, shake);
    }

    /// <summary>One soft pulse (success, new content). Full motion only.</summary>
    public static void Pulse(FrameworkElement element)
    {
        if (!MotionPolicy.AllowsTransforms(Level))
        {
            return;
        }

        ScaleTransform transform = EnsureScale(element);
        var pulse = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(360) };
        pulse.KeyFrames.Add(new EasingDoubleKeyFrame(1d, TimeSpan.Zero));
        pulse.KeyFrames.Add(new EasingDoubleKeyFrame(1.06d, TimeSpan.FromMilliseconds(150), EnterEase()));
        pulse.KeyFrames.Add(new EasingDoubleKeyFrame(1d, TimeSpan.FromMilliseconds(360), EnterEase()));

        transform.BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, pulse);
    }

    /// <summary>Hover feedback for cards: a 2 px lift. Full motion only.</summary>
    public static void HoverLift(FrameworkElement element, bool lifted)
    {
        TranslateTransform transform = Translate(element);

        if (!MotionPolicy.AllowsTransforms(Level))
        {
            transform.BeginAnimation(TranslateTransform.YProperty, null);
            transform.Y = 0;
            return;
        }

        transform.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(lifted ? -2d : 0d, MotionPolicy.StandardDuration)
            {
                EasingFunction = EnterEase(),
                FillBehavior = FillBehavior.HoldEnd
            });
    }

    /// <summary>Icon3D's +/-3 degree hover tilt through the same policy.</summary>
    public static void Tilt3D(RotateTransform transform, double angle, TimeSpan? duration = null)
    {
        if (!MotionPolicy.AllowsTransforms(Level))
        {
            transform.BeginAnimation(RotateTransform.AngleProperty, null);
            transform.Angle = 0;
            return;
        }

        transform.BeginAnimation(
            RotateTransform.AngleProperty,
            new DoubleAnimation(angle, duration ?? MotionPolicy.MicroDuration)
            {
                EasingFunction = EnterEase(),
                FillBehavior = FillBehavior.HoldEnd
            });
    }

    internal static TranslateTransform Translate(FrameworkElement element)
    {
        if (element.RenderTransform is TranslateTransform existing)
        {
            return existing;
        }

        var created = new TranslateTransform();
        element.RenderTransform = created;
        return created;
    }

    internal static ScaleTransform? FindScale(FrameworkElement element)
    {
        if (element.RenderTransform is TransformGroup group)
        {
            foreach (Transform child in group.Children)
            {
                if (child is ScaleTransform found)
                {
                    return found;
                }
            }
        }

        return element.RenderTransform as ScaleTransform;
    }

    internal static ScaleTransform EnsureScale(FrameworkElement element)
    {
        if (FindScale(element) is { } existing)
        {
            return existing;
        }

        element.RenderTransformOrigin = new Point(0.5, 0.5);
        var created = new ScaleTransform();
        element.RenderTransform = created;
        return created;
    }

    internal static void ResetScale(ScaleTransform? transform)
    {
        if (transform is null)
        {
            return;
        }

        transform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        transform.ScaleX = 1;
        transform.ScaleY = 1;
    }

    private static IEasingFunction EnterEase() => new CubicEase { EasingMode = EasingMode.EaseOut };
}

/// <summary>
/// Default policy before App startup installs the settings-aware one. It still honours the
/// OS animation switch, so a unit test or designer host never animates against the user.
/// </summary>
internal sealed class SystemMotionPolicy : IMotionPolicy
{
    public MotionLevel Level => Resolve(windowVisible: true);

    public bool AllowsFades => MotionPolicy.AllowsFades(Level);

    public bool AllowsTransforms => MotionPolicy.AllowsTransforms(Level);

    public MotionLevel Resolve(bool windowVisible) => MotionPolicy.Resolve(new MotionEnvironment(
        SystemAnimationsEnabled: System.Windows.SystemParameters.ClientAreaAnimation,
        ReduceMotion: false,
        BatterySaver: false,
        RemoteSession: false,
        LowResourceMode: false,
        WindowVisible: windowVisible));
}
