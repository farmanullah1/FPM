using System.Windows;
using PCManager.Assets;
using PCManager.Core.Models;
using PCManager.ViewModels;

namespace PCManager.Views;

/// <summary>
/// Dev-only review surface for the Icon3D inventory (plan U2). Started with --icon-gallery so the
/// normal shell is untouched. Sizes run from 20 to 96 px, the three detail levels and every tint
/// are visible side by side.
/// </summary>
public partial class IconGalleryWindow : FpmWindow
{
    private readonly IThemeService _theme;

    public IconGalleryWindow(IThemeService theme)
    {
        InitializeComponent();

        _theme = theme;
        CategoryList.ItemsSource = IconCatalog.Categories;
        Summary.Text = $"{IconCatalog.IconCount} icons in {IconCatalog.Categories.Count} categories. "
            + "Small below 28 px (flat gradient), Medium 28-55 px (extrusion), Large from 56 px "
            + "(shadow, deeper extrusion, specular crescent, rim). Hover an icon to tilt it; "
            + "the tilt is skipped when Windows animations are off.";
    }

    private void Light_Click(object sender, RoutedEventArgs e) => _theme.Apply(ThemeOption.Light);

    private void Dark_Click(object sender, RoutedEventArgs e) => _theme.Apply(ThemeOption.Dark);
}
