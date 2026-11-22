using CommunityToolkit.Mvvm.ComponentModel;

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
public sealed class NavigationItem : ObservableObject
{
    public NavigationItem(string key, string title, string subtitle, string iconKey, ViewModelBase page)
    {
        Key = key;
        Title = title;
        Subtitle = subtitle;
        IconKey = iconKey;
        Page = page;
    }

    public string Key { get; }
    public string Title { get; }
    public string Subtitle { get; }
    public string IconKey { get; }
    public ViewModelBase Page { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
