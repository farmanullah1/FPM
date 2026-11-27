using System.Windows;
using System.Windows.Controls;

namespace PCManager.Controls;

/// <summary>
/// Shared temperature tile (T2). Always rendered: an unavailable state keeps its reason visible
/// instead of hiding the surface. Compact mode keeps the toolbar to a single row.
/// </summary>
public partial class TemperatureTile : UserControl
{
    public static readonly DependencyProperty IsCompactProperty = DependencyProperty.Register(
        nameof(IsCompact), typeof(bool), typeof(TemperatureTile),
        new PropertyMetadata(false, OnCompactChanged));

    public TemperatureTile()
    {
        InitializeComponent();
    }

    public bool IsCompact
    {
        get => (bool)GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    private static void OnCompactChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var tile = (TemperatureTile)d;
        bool compact = (bool)e.NewValue;
        // Compact keeps the zone label ("System thermal zone") so a bare "Unavailable" is never
        // ambiguous; the reason stays available in the tooltip.
        tile.StatusText.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
    }
}
