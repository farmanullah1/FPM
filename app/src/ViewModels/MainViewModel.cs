using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCManager.Core;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.ViewModels;

/// <summary>
/// Shell: the six-entry navigation rail (Settings pinned at the bottom), the hosted page
/// and the theme. Pages register themselves once; navigation between them goes through
/// <see cref="NavigationService"/> so the quick-action cards do not need a reference to
/// the shell.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IThemeService _themeService;

    public MainViewModel(
        HomeViewModel home,
        ProtectionViewModel protection,
        StorageViewModel storage,
        AppsViewModel apps,
        ToolboxViewModel toolbox,
        SettingsViewModel settingsPage,
        NavigationService navigation,
        ISettingsService settings,
        IThemeService themeService)
    {
        _settings = settings;
        _themeService = themeService;

        Pages = new ObservableCollection<NavigationItem>
        {
            new(PageKeys.Home, "Home", "Boost and health", "Icon.Home", home),
            new(PageKeys.Protection, "Protection", "Security and updates", "Icon.Health", protection),
            new(PageKeys.Storage, "Storage", "Cleanup and disks", "Icon.Storage", storage),
            new(PageKeys.Apps, "Apps", "Processes and startup", "Icon.Process", apps),
            new(PageKeys.Toolbox, "Toolbox", "Everyday utilities", "Icon.Toolbox", toolbox)
        };

        SettingsPage = new NavigationItem(PageKeys.Settings, "Settings", "Preferences and about", "Icon.Settings", settingsPage);
        NavigationItems = new ObservableCollection<NavigationItem>(Pages) { SettingsPage };

        // Bound to the pinned bottom ListBox in the shell.
        SettingsItems = new ObservableCollection<NavigationItem> { SettingsPage };

        navigation.Handler = SelectPage;

        _themeService.Apply(settings.Current.Theme);

        _currentPage = Pages[0].Page;
        _selectedPage = Pages[0];
        Pages[0].IsSelected = true;
    }

    /// <summary>The five main tabs.</summary>
    public ObservableCollection<NavigationItem> Pages { get; }

    /// <summary>Settings is pinned under the rail separator.</summary>
    public NavigationItem SettingsPage { get; }

    /// <summary>All six entries, in rail order.</summary>
    public ObservableCollection<NavigationItem> NavigationItems { get; }

    /// <summary>Single-item collection for the pinned Settings entry.</summary>
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
            if (value is null || !SetProperty(ref _selectedPage, value))
            {
                return;
            }

            foreach (NavigationItem item in NavigationItems)
            {
                item.IsSelected = ReferenceEquals(item, value);
            }

            CurrentPage = value.Page;
            _ = value.Page.LoadAsync();
        }
    }

    /// <summary>Called by the window once it is on screen.</summary>
    public Task LoadAsync() => CurrentPage.LoadAsync();

    /// <summary>Navigation target for <see cref="NavigationService"/> (quick-action cards).</summary>
    private void SelectPage(string pageKey)
    {
        NavigationItem? target = NavigationItems.FirstOrDefault(i => i.Key == pageKey);

        if (target is not null)
        {
            SelectedPage = target;
        }
    }
}
