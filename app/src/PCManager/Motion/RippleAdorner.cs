using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PCManager.Motion;

/// <summary>
/// A press ripple drawn in an adorner layer, so the button template and its layout stay
/// untouched. The circle expands to the button's farthest corner while fading out, then
/// removes itself.
/// </summary>
internal sealed class RippleAdorner : Adorner
{
    private static readonly DependencyProperty RadiusProperty = DependencyProperty.Register(
        nameof(Radius),
        typeof(double),
        typeof(RippleAdorner),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender, OnRadiusChanged));

    private readonly Point _origin;
    private readonly Brush _brush;
    private readonly double _targetRadius;
    private readonly DrawingVisual _visual = new();

    public RippleAdorner(UIElement adornedElement, Point origin, Color tint) : base(adornedElement)
    {
        _origin = origin;
        IsHitTestVisible = false;

        double width = adornedElement.RenderSize.Width;
        double height = adornedElement.RenderSize.Height;
        _targetRadius = Math.Max(Math.Max(origin.X, width - origin.X), Math.Max(origin.Y, height - origin.Y)) + 8;

        var brush = new SolidColorBrush(Color.FromArgb(0x40, tint.R, tint.G, tint.B));
        brush.Freeze();
        _brush = brush;

        AddVisualChild(_visual);
        Redraw();
    }

    private double Radius
    {
        get => (double)GetValue(RadiusProperty);
        set => SetValue(RadiusProperty, value);
    }

    /// <summary>Starts the 360 ms ripple; the animation removes the adorner when it ends.</summary>
    public void Play()
    {
        var grow = new DoubleAnimation(0d, _targetRadius, TimeSpan.FromMilliseconds(360))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        var fade = new DoubleAnimation(1d, 0d, TimeSpan.FromMilliseconds(360))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn },
            Completed = (_, _) => Detach()
        };

        BeginAnimation(RadiusProperty, grow);
        BeginAnimation(OpacityProperty, fade);
    }

    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index) => _visual;

    private static void OnRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((RippleAdorner)d).Redraw();

    private void Redraw()
    {
        using DrawingContext context = _visual.RenderOpen();
        context.DrawEllipse(_brush, null, _origin, Radius, Radius);
    }

    private void Detach() => AdornerLayer.GetAdornerLayer(AdornedElement)?.Remove(this);
}
