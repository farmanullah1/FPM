using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCManager.Core;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.ViewModels;

/// <summary>One running user application with its own End command.</summary>
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
    public string MemoryText => $"{Process.MemoryBytes / (1024.0 * 1024.0):0.0} MB";
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

/// <summary>
/// Apps: running user applications (ending is refused for protected processes), startup
/// entries with real On/Off toggles, and the installed-application list with search.
/// </summary>
public sealed class AppsViewModel : ViewModelBase, IDisposable
{
    private const int MaxRows = 60;

    private readonly IProcessService _processes;
    private readonly IInstalledAppService _installedApps;
    private readonly ISettingsService _settings;
    private readonly IAppLogger _logger;
    private readonly DispatcherTimer _timer;

    private IReadOnlyList<InstalledApp> _allInstalled = Array.Empty<InstalledApp>();
    private bool _refreshing;

    public AppsViewModel(
        IProcessService processes,
        IInstalledAppService installedApps,
        ISettingsService settings,
        IAppLogger logger)
    {
        _processes = processes;
        _installedApps = installedApps;
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

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand RefreshStartupCommand { get; }

    private string _appSearch = string.Empty;
    public string AppSearch
    {
        get => _appSearch;
        set
        {
            if (SetProperty(ref _appSearch, value))
            {
                ApplyInstalledFilter();
            }
        }
    }

    private string _installedSummary = "Reading installed applications...";
    public string InstalledSummary
    {
        get => _installedSummary;
        private set => SetProperty(ref _installedSummary, value);
    }

    private string _processSummary = "Reading running applications...";
    public string ProcessSummary
    {
        get => _processSummary;
        private set => SetProperty(ref _processSummary, value);
    }

    public override async Task LoadAsync()
    {
        await RefreshProcessesAsync();
        await LoadStartupAsync();
        await LoadInstalledAsync();
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
                .Where(p => !p.IsSystem)
                .Where(p => !AppInfo.IsProtectedProcessName(p.Name))
                .Where(p => !string.IsNullOrWhiteSpace(p.Name))
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

    private async Task UninstallAsync(InstalledAppViewModel app)
    {
        OperationResult result = await _installedApps.UninstallAsync(app.App);
        StatusMessage = result.Message;
    }

    public void Dispose() => _timer.Stop();
}
