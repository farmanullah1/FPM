using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using PCManager.Core;

namespace PCManager.Motion;

/// <summary>
/// Attached behaviours and value primitives for the U4 kit. Every entry point consults
/// <see cref="Motion.Policy"/>; with Reduced motion the value still updates, just without easing.
/// </summary>
public static partial class Motion
{
    // ---------------------------------------------------------------- hover lift

    public static readonly DependencyProperty HoverLiftProperty = DependencyProperty.RegisterAttached(
        "HoverLift",
        typeof(bool),
        typeof(Motion),
        new PropertyMetadata(false, OnHoverLiftChanged));

    public static void SetHoverLift(DependencyObject element, bool value) => element.SetValue(HoverLiftProperty, value);

    public static bool GetHoverLift(DependencyObject element) => (bool)element.GetValue(HoverLiftProperty);

    private static void OnHoverLiftChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
        {
            return;
        }

        element.MouseEnter -= OnHoverLiftEnter;
        element.MouseLeave -= OnHoverLiftLeave;

        if (e.NewValue is true)
        {
            element.MouseEnter += OnHoverLiftEnter;
            element.MouseLeave += OnHoverLiftLeave;
        }
    }

    private static void OnHoverLiftEnter(object sender, System.Windows.Input.MouseEventArgs e) =>
        HoverLift((FrameworkElement)sender, lifted: true);

    private static void OnHoverLiftLeave(object sender, System.Windows.Input.MouseEventArgs e) =>
        HoverLift((FrameworkElement)sender, lifted: false);

    // ---------------------------------------------------------------- press ripple

    public static readonly DependencyProperty RippleProperty = DependencyProperty.RegisterAttached(
        "Ripple",
        typeof(bool),
        typeof(Motion),
        new PropertyMetadata(false, OnRippleChanged));

    public static void SetRipple(DependencyObject element, bool value) => element.SetValue(RippleProperty, value);

    public static bool GetRipple(DependencyObject element) => (bool)element.GetValue(RippleProperty);

    private static void OnRippleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ButtonBase button)
        {
            return;
        }

        button.PreviewMouseLeftButtonDown -= OnRipplePressed;

        if (e.NewValue is true)
        {
            button.PreviewMouseLeftButtonDown += OnRipplePressed;
        }
    }

    private static void OnRipplePressed(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!MotionPolicy.AllowsTransforms(Level) || sender is not ButtonBase button)
        {
            return;
        }

        Point origin = e.GetPosition(button);
        AdornerLayer? layer = AdornerLayer.GetAdornerLayer(button);

        if (layer is null)
        {
            return;
        }

        Color tint = (button.TryFindResource("App.Accent") as SolidColorBrush)?.Color ?? Colors.Goldenrod;
        var adorner = new RippleAdorner(button, origin, tint);
        layer.Add(adorner);
        adorner.Play();
    }

    // ---------------------------------------------------------------- count up

    private static readonly ConditionalWeakTable<TextBlock, Func<double, string>> CountFormats = new();

    private static readonly DependencyProperty CountValueProperty = DependencyProperty.RegisterAttached(
        "CountValue",
        typeof(double),
        typeof(Motion),
        new PropertyMetadata(0d, OnCountValueChanged));

    /// <summary>Counts a number up (or down) with the shared easing; text comes from the formatter.</summary>
    public static void CountUp(TextBlock target, double from, double to, Func<double, string> format, TimeSpan? duration = null)
    {
        if (!CountFormats.TryGetValue(target, out _))
        {
            CountFormats.Add(target, format);
        }
        else
        {
            CountFormats.Remove(target);
            CountFormats.Add(target, format);
        }

        if (!MotionPolicy.AllowsTransforms(Level))
        {
            target.BeginAnimation(CountValueProperty, null);
            target.Text = format(to);
            return;
        }

        target.BeginAnimation(
            CountValueProperty,
            new DoubleAnimation(from, to, duration ?? MotionPolicy.StandardDuration)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.HoldEnd
            });
    }

    private static void OnCountValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBlock target && CountFormats.TryGetValue(target, out Func<double, string>? format))
        {
            target.Text = format((double)e.NewValue);
        }
    }

    // ---------------------------------------------------------------- progress

    /// <summary>Eases a progress value; Reduced/None jumps straight to the value.</summary>
    public static void ProgressTween(RangeBase bar, double to, TimeSpan? duration = null)
    {
        if (!MotionPolicy.AllowsTransforms(Level))
        {
            bar.BeginAnimation(RangeBase.ValueProperty, null);
            bar.Value = to;
            return;
        }

        bar.BeginAnimation(
            RangeBase.ValueProperty,
            new DoubleAnimation(to, duration ?? MotionPolicy.StandardDuration)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.HoldEnd
            });
    }

    // ---------------------------------------------------------------- shimmer

    /// <summary>Loading shimmer: a slow opacity breath. Full motion only; otherwise static.</summary>
    public static void StartShimmer(FrameworkElement element, TimeSpan? period = null)
    {
        if (!MotionPolicy.AllowsTransforms(Level))
        {
            StopShimmer(element);
            return;
        }

        var breath = new DoubleAnimation(1d, 0.55d, period ?? TimeSpan.FromMilliseconds(900))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };

        element.BeginAnimation(UIElement.OpacityProperty, breath);
    }

    public static void StopShimmer(FrameworkElement element)
    {
        element.BeginAnimation(UIElement.OpacityProperty, null);
        element.Opacity = 1;
    }
}
