using System.Windows;
using System.Windows.Controls;

namespace PCManager.Controls;

/// <summary>
/// Shared battery tile (T1): binds to <see cref="ViewModels.BatteryStatusViewModel"/> and hides
/// itself when Windows reports no battery. The compact form drops the time/health line so the
/// floating toolbar stays a single row.
/// </summary>
public partial class BatteryTile : UserControl
{
    public static readonly DependencyProperty IsCompactProperty = DependencyProperty.Register(
        nameof(IsCompact), typeof(bool), typeof(BatteryTile),
        new PropertyMetadata(false, OnCompactChanged));

    public BatteryTile()
    {
        InitializeComponent();
    }

    public bool IsCompact
    {
        get => (bool)GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    private static void OnCompactChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((BatteryTile)d).DetailRow.Visibility = (bool)e.NewValue ? Visibility.Collapsed : Visibility.Visible;
}
