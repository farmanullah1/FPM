using System.Windows;
using Microsoft.Win32;
using PCManager.Controls;
using PCManager.Core.Models;
using PCManager.ViewModels;

namespace PCManager.Services;

/// <summary>
/// Swaps the theme resource dictionary at index 0 of the application resources.
/// Light.xaml and Dark.xaml define the same brush keys, so every control binds through
/// DynamicResource and updates instantly.
/// </summary>
public sealed class ThemeService : IThemeService
{
    /// <summary>Raised after a theme swap; true when the dark dictionary is active.</summary>
    public static event Action<bool>? ThemeApplied;

    private static readonly Uri LightTheme = new("Styles/Themes/Light.xaml", UriKind.Relative);
    private static readonly Uri DarkTheme = new("Styles/Themes/Dark.xaml", UriKind.Relative);

    public ThemeOption Current { get; private set; } = ThemeOption.System;

    public void Apply(ThemeOption option)
    {
        Current = option;

        bool dark = option switch
        {
            ThemeOption.Dark => true,
            ThemeOption.Light => false,
            _ => IsSystemDark()
        };

        var dictionary = new ResourceDictionary { Source = dark ? DarkTheme : LightTheme };
        var merged = Application.Current.Resources.MergedDictionaries;

        if (merged.Count == 0)
        {
            merged.Add(dictionary);
        }
        else
        {
            merged[0] = dictionary;
        }

        // Icon3D bakes the palette into its frozen drawings, so it has to rebuild.
        Icon3D.NotifyThemeChanged();

        // Windows drawing the frame (rounded corners, dark system menu) must follow the app theme.
        ThemeApplied?.Invoke(dark);
    }

    /// <summary>Windows stores the app theme under Personalize\AppsUseLightTheme (0 = dark).</summary>
    private static bool IsSystemDark()
    {
        try
        {
            object? value = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme",
                1);

            return value is int light && light == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
