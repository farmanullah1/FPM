using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PCManager.Core;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.ViewModels;

/// <summary>
/// Shell: the six-entry navigation rail (Settings pinned at the bottom), the hosted page
/// and the theme. Navigation from a page goes through <see cref="NavigationService"/> so
/// the quick-action cards never reference the shell.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    private readonly IThemeService _themeService;

    public MainViewModel(
        HomeViewModel home,
        ProtectionViewModel protection,
        StorageViewModel storage,
        AppsViewModel apps,
        ToolboxViewModel toolbox,
        MemoryViewModel memory,
        SettingsViewModel settingsPage,
        NavigationService navigation,
        ISettingsService settings,
        IThemeService themeService)
    {
        _themeService = themeService;

        Pages = new ObservableCollection<NavigationItem>
        {
            new(PageKeys.Home, "Home", "Boost and health", "Icon.Home", home),
            new(PageKeys.Memory, "Memory", "RAM and process relief", "Icon.Performance", memory),
            new(PageKeys.Protection, "Protection", "Security and updates", "Icon.Health", protection),
            new(PageKeys.Storage, "Storage", "Cleanup and disks", "Icon.Storage", storage),
            new(PageKeys.Apps, "Apps", "Processes and startup", "Icon.Process", apps),
            new(PageKeys.Toolbox, "Toolbox", "Everyday utilities", "Icon.Toolbox", toolbox)
        };

        // Settings is pinned under the rail separator, so it lives in its own one-item list.
        SettingsItems = new ObservableCollection<NavigationItem>
        {
            new(PageKeys.Settings, "Settings", "Preferences and about", "Icon.Settings", settingsPage)
        };

        navigation.Handler = SelectPage;
        _themeService.Apply(settings.Current.Theme);

        _currentPage = Pages[0].Page;
        _selectedPage = Pages[0];
    }

    public ObservableCollection<NavigationItem> Pages { get; }

    public ObservableCollection<NavigationItem> SettingsItems { get; }

    public string ProductName => AppInfo.ProductName;

    public string VersionText => $"Version {AppInfo.Version}";

    private ViewModelBase _currentPage;
    public ViewModelBase CurrentPage
    {
        get => _currentPage;
        private set => SetProperty(ref _currentPage, value);
    }

    private NavigationItem _selectedPage;
    public NavigationItem SelectedPage
    {
        get => _selectedPage;
        set
        {
            if (value is null || ReferenceEquals(value, _selectedPage))
            {
                return;
            }

            SetProperty(ref _selectedPage, value);
            CurrentPage = value.Page;
            _ = value.Page.LoadAsync();
        }
    }

    /// <summary>Called by the window once it is on screen.</summary>
    public Task LoadAsync() => CurrentPage.LoadAsync();

    private void SelectPage(string pageKey)
    {
        NavigationItem? target = Pages.Concat(SettingsItems).FirstOrDefault(i => i.Key == pageKey);

        if (target is not null)
        {
            SelectedPage = target;
        }
    }
}
