using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCManager.Core;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.ViewModels;

/// <summary>
/// Shell: the six-entry navigation rail (Settings pinned at the bottom), the hosted page
/// and the theme. Navigation from a page goes through <see cref="NavigationService"/> so
/// the quick-action cards never reference the shell.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
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

        ToggleCommandPaletteCommand = new RelayCommand(ToggleCommandPalette);
        CloseCommandPaletteCommand = new RelayCommand(() => IsCommandPaletteOpen = false);
        ExecutePaletteCommand = new RelayCommand<CommandPaletteEntry>(ExecuteCommand);

        BuildCommandPalette();
    }

    public ObservableCollection<NavigationItem> Pages { get; }

    public ObservableCollection<NavigationItem> SettingsItems { get; }

    public ObservableCollection<CommandPaletteEntry> FilteredCommands { get; } = new();
    private readonly List<CommandPaletteEntry> _allCommands = new();

    public IRelayCommand ToggleCommandPaletteCommand { get; }
    public IRelayCommand CloseCommandPaletteCommand { get; }
    public IRelayCommand<CommandPaletteEntry> ExecutePaletteCommand { get; }

    [ObservableProperty]
    private bool _isCommandPaletteOpen;

    [ObservableProperty]
    private string _commandPaletteQuery = string.Empty;

    partial void OnCommandPaletteQueryChanged(string value)
    {
        FilterCommands();
    }

    private void ToggleCommandPalette()
    {
        IsCommandPaletteOpen = !IsCommandPaletteOpen;
        if (IsCommandPaletteOpen)
        {
            CommandPaletteQuery = string.Empty;
            FilterCommands();
        }
    }

    private void ExecuteCommand(CommandPaletteEntry? entry)
    {
        if (entry is null) return;
        IsCommandPaletteOpen = false;
        entry.Action.Invoke();
    }

    private void FilterCommands()
    {
        FilteredCommands.Clear();
        string q = CommandPaletteQuery.Trim();
        var matches = string.IsNullOrWhiteSpace(q)
            ? _allCommands
            : _allCommands.Where(c => c.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                                      c.Category.Contains(q, StringComparison.OrdinalIgnoreCase));

        foreach (CommandPaletteEntry item in matches)
        {
            FilteredCommands.Add(item);
        }
    }

    private void BuildCommandPalette()
    {
        _allCommands.Clear();
        _allCommands.Add(new CommandPaletteEntry("Navigate to Home", "Navigation", "Ctrl+1", () => SelectPage(PageKeys.Home)));
        _allCommands.Add(new CommandPaletteEntry("Navigate to Memory", "Navigation", "Ctrl+2", () => SelectPage(PageKeys.Memory)));
        _allCommands.Add(new CommandPaletteEntry("Navigate to Protection", "Navigation", "Ctrl+3", () => SelectPage(PageKeys.Protection)));
        _allCommands.Add(new CommandPaletteEntry("Navigate to Storage", "Navigation", "Ctrl+4", () => SelectPage(PageKeys.Storage)));
        _allCommands.Add(new CommandPaletteEntry("Navigate to Apps", "Navigation", "Ctrl+5", () => SelectPage(PageKeys.Apps)));
        _allCommands.Add(new CommandPaletteEntry("Navigate to Toolbox", "Navigation", "Ctrl+6", () => SelectPage(PageKeys.Toolbox)));
        _allCommands.Add(new CommandPaletteEntry("Navigate to Settings", "Navigation", "Ctrl+,", () => SelectPage(PageKeys.Settings)));
        _allCommands.Add(new CommandPaletteEntry("One-Click Cleanup", "Action", "Enter", () => SelectPage(PageKeys.Home)));
        _allCommands.Add(new CommandPaletteEntry("Analyze Component Store (DISM)", "Action", string.Empty, () => SelectPage(PageKeys.Storage)));
        _allCommands.Add(new CommandPaletteEntry("Downloads Categorization Manager", "Action", string.Empty, () => SelectPage(PageKeys.Storage)));
        _allCommands.Add(new CommandPaletteEntry("App Leftovers Deep Cleaner", "Action", string.Empty, () => SelectPage(PageKeys.Apps)));
        _allCommands.Add(new CommandPaletteEntry("Test Network Diagnostics & Latency", "Action", string.Empty, () => SelectPage(PageKeys.Protection)));
        _allCommands.Add(new CommandPaletteEntry("Flush DNS Resolver Cache", "Action", string.Empty, () => SelectPage(PageKeys.Protection)));
        _allCommands.Add(new CommandPaletteEntry("Trigger Windows Defender Quick Scan", "Action", string.Empty, () => SelectPage(PageKeys.Protection)));
        FilterCommands();
    }

    public string ProductName => AppInfo.ProductName;

    public string VersionText => $"Version {AppInfo.Version}";

    [ObservableProperty]
    private bool _isExpandedSidebar;

    [ObservableProperty]
    private double _currentWindowWidth = 560;

    public void UpdateResponsiveLayout(double width)
    {
        CurrentWindowWidth = width;
        IsExpandedSidebar = width >= 960;
    }

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
