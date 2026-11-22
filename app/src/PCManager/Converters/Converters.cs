using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using PCManager.Core.Models;

namespace PCManager.Converters;

/// <summary>Turns a resource key such as "Icon.Health" into the Geometry in App resources.</summary>
public sealed class IconKeyToGeometryConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string key && Application.Current.TryFindResource(key) is Geometry geometry)
        {
            return geometry;
        }

        return Geometry.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Maps a health status to the matching accent brush.</summary>
public sealed class StatusToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string key = value switch
        {
            HealthItemStatus.Critical => "App.Critical",
            HealthItemStatus.Warning => "App.Warning",
            _ => "App.Success"
        };

        return Application.Current.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Maps a boolean to the success/warning brush (used by the protection cards).</summary>
public sealed class HealthyToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool healthy = value is true;
        return Application.Current.TryFindResource(healthy ? "App.Success" : "App.Warning") as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Inverts a boolean (for "enabled while not busy").</summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not true;
}
