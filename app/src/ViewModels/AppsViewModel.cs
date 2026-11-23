using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCManager.Core;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.ViewModels;

/// <summary>One running application with its own End command.</summary>
public sealed class RunningAppViewModel : ObservableObject
{
    private readonly Func<RunningAppViewModel, Task> _end;

    public RunningAppViewModel(ProcessInfo process, Func<RunningAppViewModel, Task> end)
    {
        Process = process;
        _end = end;
        EndCommand = new AsyncRelayCommand(() => _end(this));
    }

    public ProcessInfo Process { get; }
    public int ProcessId => Process.ProcessId;
    public string Name => Process.Name;
    public string PidText => $"PID {Process.ProcessId}";
    public string MemoryText => Process.MemoryText;
    public string CpuText => Process.CpuText;
    public AsyncRelayCommand EndCommand { get; }
}

/// <summary>One installed application with its uninstall command.</summary>
public sealed class InstalledAppViewModel : ObservableObject
{
    private readonly Func<InstalledAppViewModel, Task> _uninstall;

    public InstalledAppViewModel(InstalledApp app, Func<InstalledAppViewModel, Task> uninstall)
    {
        App = app;
        _uninstall = uninstall;
        UninstallCommand = new AsyncRelayCommand(() => _uninstall(this));
    }

    public InstalledApp App { get; }
    public string Name => App.Name;
    public string Subtitle => App.Subtitle;
    public string VersionText => string.IsNullOrWhiteSpace(App.Version) ? string.Empty : $"v{App.Version}";
    public string SizeText => App.SizeText;
    public AsyncRelayCommand UninstallCommand { get; }
}

/// <summary>One Store/MSIX package with a confirmation-gated uninstall command.</summary>
public sealed class StoreAppViewModel : ObservableObject
{
    private readonly Func<StoreAppViewModel, Task> _uninstall;

    public StoreAppViewModel(AppxPackage package, Func<StoreAppViewModel, Task> uninstall)
    {
        Package = package;
        _uninstall = uninstall;
        UninstallCommand = new AsyncRelayCommand(() => _uninstall(this));
    }

    public AppxPackage Package { get; }
    public string Name => Package.Name;
    public string Subtitle => Package.Subtitle;
    public string PackageFullName => Package.PackageFullName;
    public AsyncRelayCommand UninstallCommand { get; }
}

/// <summary>
/// Apps: running user applications (ending is refused for protected processes), startup
/// entries with real On/Off toggles, installed desktop apps and Store/MSIX packages.
/// </summary>
public sealed partial class AppsViewModel : ViewModelBase, IDisposable
{
    private const int MaxRows = 60;

    private readonly IProcessService _processes;
    private readonly IInstalledAppService _installedApps;
    private readonly IAppxPackageService _appxPackages;
    private readonly IUserConfirmationService _confirmation;
    private readonly ISettingsService _settings;
    private readonly IAppLogger _logger;
    private readonly DispatcherTimer _timer;

    private IReadOnlyList<InstalledApp> _allInstalled = Array.Empty<InstalledApp>();
    private IReadOnlyList<AppxPackage> _allStore = Array.Empty<AppxPackage>();
    private bool _refreshing;

    public AppsViewModel(
        IProcessService processes,
        IInstalledAppService installedApps,
        IAppxPackageService appxPackages,
        IUserConfirmationService confirmation,
        ISettingsService settings,
        IAppLogger logger)
    {
        _processes = processes;
        _installedApps = installedApps;
        _appxPackages = appxPackages;
        _confirmation = confirmation;
        _settings = settings;
        _logger = logger;

        RefreshCommand = new AsyncRelayCommand(LoadAsync, () => !IsBusy);
        RefreshStartupCommand = new AsyncRelayCommand(LoadStartupAsync, () => !IsBusy);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _timer.Tick += async (_, _) =>
        {
            if (_settings.Current.AutoRefreshProcessList && !_refreshing && !IsBusy)
            {
                await RefreshProcessesAsync();
            }
        };
        _timer.Start();
    }

    public ObservableCollection<RunningAppViewModel> RunningApps { get; } = new();
    public ObservableCollection<StartupAppViewModel> StartupApps { get; } = new();
    public ObservableCollection<InstalledAppViewModel> InstalledApps { get; } = new();
    public ObservableCollection<StoreAppViewModel> StoreApps { get; } = new();

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand RefreshStartupCommand { get; }

    [ObservableProperty]
    private string _appSearch = string.Empty;

    [ObservableProperty]
    private string _installedSummary = "Reading installed applications...";

    [ObservableProperty]
    private string _storeAppsSummary = "Reading Store/MSIX packages...";

    [ObservableProperty]
    private string _processSummary = "Reading running applications...";

    partial void OnAppSearchChanged(string value)
    {
        ApplyInstalledFilter();
        ApplyStoreFilter();
    }

    public override async Task LoadAsync()
    {
        await RefreshProcessesAsync();
        await LoadStartupAsync();
        await LoadInstalledAsync();
        await LoadStoreAppsAsync();
    }

    protected override void OnBusyChanged()
    {
        RefreshCommand.NotifyCanExecuteChanged();
        RefreshStartupCommand.NotifyCanExecuteChanged();
    }

    private async Task RefreshProcessesAsync()
    {
        if (_refreshing)
        {
            return;
        }

        _refreshing = true;

        try
        {
            IReadOnlyList<ProcessInfo> all = await _processes.GetProcessesAsync();

            // Only user-facing applications, and never ourselves or Microsoft PC Manager.
            List<ProcessInfo> apps = all
                .Where(p => !p.IsSystem && !AppInfo.IsProtectedProcessName(p.Name) && !string.IsNullOrWhiteSpace(p.Name))
                .OrderByDescending(p => p.MemoryBytes)
                .Take(MaxRows)
                .ToList();

            RunningApps.Clear();
            foreach (ProcessInfo process in apps)
            {
                RunningApps.Add(new RunningAppViewModel(process, EndAsync));
            }

            ProcessSummary = $"{apps.Count} applications · {apps.Sum(a => a.MemoryBytes) / (1024.0 * 1024.0 * 1024.0):0.00} GB in use";
        }
        catch (Exception ex)
        {
            ProcessSummary = $"Could not read processes: {ex.Message}";
            _logger.Error("Process refresh failed.", ex);
        }
        finally
        {
            _refreshing = false;
        }
    }

    private async Task EndAsync(RunningAppViewModel app)
    {
        OperationResult result = await _processes.EndProcessAsync(app.ProcessId);
        StatusMessage = result.Message;

        if (result.Success)
        {
            await RefreshProcessesAsync();
        }
    }

    private async Task LoadStartupAsync()
    {
        try
        {
            IReadOnlyList<StartupApp> apps = await _processes.GetStartupAppsAsync();

            StartupApps.Clear();
            foreach (StartupApp app in apps)
            {
                StartupApps.Add(new StartupAppViewModel(app, _processes, message => StatusMessage = message));
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not read startup programs: {ex.Message}";
            _logger.Error("Startup refresh failed.", ex);
        }
    }

    private async Task LoadInstalledAsync()
    {
        try
        {
            _allInstalled = await _installedApps.GetInstalledAppsAsync();
            ApplyInstalledFilter();
        }
        catch (Exception ex)
        {
            InstalledSummary = $"Could not read installed apps: {ex.Message}";
            _logger.Error("Installed apps refresh failed.", ex);
        }
    }

    private async Task LoadStoreAppsAsync()
    {
        try
        {
            _allStore = await _appxPackages.GetPackagesAsync();
            ApplyStoreFilter();
        }
        catch (Exception ex)
        {
            StoreAppsSummary = $"Could not read Store/MSIX packages: {ex.Message}";
            _logger.Error("Store/MSIX package refresh failed.", ex);
        }
    }

    private void ApplyInstalledFilter()
    {
        string query = AppSearch.Trim();

        IEnumerable<InstalledApp> filtered = _allInstalled;

        if (query.Length > 0)
        {
            filtered = filtered.Where(a =>
                a.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                a.Publisher.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        InstalledApps.Clear();
        foreach (InstalledApp app in filtered.Take(200))
        {
            InstalledApps.Add(new InstalledAppViewModel(app, UninstallAsync));
        }

        InstalledSummary = $"{_allInstalled.Count} installed · showing {InstalledApps.Count}";
    }

    private void ApplyStoreFilter()
    {
        string query = AppSearch.Trim();

        IEnumerable<AppxPackage> filtered = _allStore;

        if (query.Length > 0)
        {
            filtered = filtered.Where(package =>
                package.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                package.Publisher.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                package.Version.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        StoreApps.Clear();
        foreach (AppxPackage package in filtered.Take(200))
        {
            StoreApps.Add(new StoreAppViewModel(package, UninstallStoreAsync));
        }

        StoreAppsSummary = $"{_allStore.Count} Store package(s) · showing {StoreApps.Count}";
    }

    private async Task UninstallAsync(InstalledAppViewModel app)
    {
        if (!_confirmation.Confirm(
                "Uninstall application",
                $"Start the uninstaller for {app.Name}?\n\nThe installer controls the rest of the process."))
        {
            return;
        }

        StatusMessage = $"The uninstaller for {app.Name} is starting. Waiting for it to finish...";

        OperationResult result = await _installedApps.UninstallAsync(app.App);
        StatusMessage = result.Message;

        // The uninstaller may have changed the list even when it did not finish cleanly.
        await LoadInstalledAsync();
    }

    private async Task UninstallStoreAsync(StoreAppViewModel app)
    {
        if (!app.Package.CanUninstall)
        {
            StatusMessage = "That package is protected or is not removable by this app. Use Windows Settings.";
            return;
        }

        if (!_confirmation.Confirm(
                "Uninstall Store app",
                $"Remove {app.Name} for your current Windows account?\n\n{app.PackageFullName}\n\nThe package must be reinstalled from the Store or its installer."))
        {
            return;
        }

        OperationResult result = await _appxPackages.UninstallAsync(app.Package);
        StatusMessage = result.Message;

        if (result.Success)
        {
            await LoadInstalledAsync();
            await LoadStoreAppsAsync();
        }
    }

    public void Dispose() => _timer.Stop();
}
