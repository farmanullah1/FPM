using PCManager.Core.Models;

namespace PCManager.ViewModels;

/// <summary>
/// Applies a theme to the shell. Implemented in the WPF project because it swaps
/// resource dictionaries.
/// </summary>
public interface IThemeService
{
    ThemeOption Current { get; }

    /// <summary>Applies Light/Dark. Default and System resolve to the Windows app theme.</summary>
    void Apply(ThemeOption option);
}
