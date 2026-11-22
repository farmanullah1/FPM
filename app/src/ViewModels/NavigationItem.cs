namespace PCManager.ViewModels;

/// <summary>Page identifiers used by <see cref="INavigationService"/>.</summary>
public static class PageKeys
{
    public const string Home = "home";
    public const string Protection = "protection";
    public const string Storage = "storage";
    public const string Apps = "apps";
    public const string Toolbox = "toolbox";
    public const string Settings = "settings";
}

/// <summary>Cross-page navigation, implemented by the shell view model.</summary>
public interface INavigationService
{
    void Navigate(string pageKey);
}

/// <summary>One entry of the left navigation rail.</summary>
public sealed class NavigationItem(string key, string title, string subtitle, string iconKey, ViewModelBase page)
{
    public string Key { get; } = key;
    public string Title { get; } = title;
    public string Subtitle { get; } = subtitle;
    public string IconKey { get; } = iconKey;
    public ViewModelBase Page { get; } = page;
}
