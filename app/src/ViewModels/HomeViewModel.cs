using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCManager.Core;
using PCManager.Core.Formatting;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.ViewModels;

/// <summary>
/// Home: the contextual tip, the PC Boost hero card (live memory load + temporary-file
/// size), the Boost action, the Smart Boost switch, the 2x2 quick-action grid and the
/// compact capture strip.
/// </summary>
public sealed partial class HomeViewModel : ViewModelBase, IDisposable
{
    private const string TipsUrl =
        "https://support.microsoft.com/en-us/windows/keyboard-shortcuts-in-windows-dcc61a57-8ff0-cffe-9796-cb9706c75eec";

    private readonly IPcBoostService _boost;
    private readonly ITempFileCleaner _tempCleaner;
    private readonly ISettingsService _settings;
    private readonly INavigationService _navigation;
    private readonly IProcessService _processes;
    private readonly IStorageService _storage;
    private readonly IToolboxService _toolbox;
    private readonly IToolLauncher _launcher;
    private readonly IAppLogger _logger;
    private readonly DispatcherTimer _timer;

    private DateTime _lastSmartBoost = DateTime.MinValue;

    public HomeViewModel(
        IPcBoostService boost,
        ITempFileCleaner tempCleaner,
        ISettingsService settings,
        INavigationService navigation,
        IProcessService processes,
        IStorageService storage,
        IToolboxService toolbox,
        IToolLauncher launcher,
        IAppLogger logger)
    {
        _boost = boost;
        _tempCleaner = tempCleaner;
        _settings = settings;
        _navigation = navigation;
        _processes = processes;
        _storage = storage;
        _toolbox = toolbox;
        _launcher = launcher;
        _logger = logger;

        BoostCommand = new AsyncRelayCommand(BoostAsync, () => !IsBusy);
        CaptureCommand = new AsyncRelayCommand(CaptureAsync, () => !IsBusy);
        OpenProtectionCommand = new RelayCommand(() => _navigation.Navigate(PageKeys.Protection));
        OpenProcessesCommand = new RelayCommand(() => _navigation.Navigate(PageKeys.Apps));
        OpenCleanupCommand = new RelayCommand(() => _navigation.Navigate(PageKeys.Storage));
        OpenStartupCommand = new RelayCommand(() => _navigation.Navigate(PageKeys.Apps));
        OpenTipsCommand = new RelayCommand(OpenTips);
        DismissTipCommand = new RelayCommand(() => IsTipVisible = false);

        StatusMessage = "Ready.";

        // Live gauge: GlobalMemoryStatusEx is allocation-free, so a 4 s poll is cheap.
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _timer.Tick += (_, _) => OnTick();
        _timer.Start();
    }

    public AsyncRelayCommand BoostCommand { get; }
    public AsyncRelayCommand CaptureCommand { get; }
    public RelayCommand OpenProtectionCommand { get; }
    public RelayCommand OpenProcessesCommand { get; }
    public RelayCommand OpenCleanupCommand { get; }
    public RelayCommand OpenStartupCommand { get; }
    public RelayCommand OpenTipsCommand { get; }
    public RelayCommand DismissTipCommand { get; }

    [ObservableProperty]
    private bool _isTipVisible = true;

    [ObservableProperty]
    private string _boostStatusText = "0 KB of temporary files released so far.";

    [ObservableProperty]
    private string _processCountText = "-";

    [ObservableProperty]
    private string _diskUsageText = "-";

    [ObservableProperty]
    private double _diskUsedPercent;

    [ObservableProperty]
    private double _loadPercent;

    [ObservableProperty]
    private string _availableText = "-";

    [ObservableProperty]
    private string _tempFilesText = "-";

    [ObservableProperty]
    private string _resultText = "Nothing has been optimised yet.";

    public string LoadText => $"{LoadPercent:0}%";

    /// <summary>Boost button caption; the command itself disables the button while busy.</summary>
    public string BoostButtonText => IsBusy ? "Optimizing..." : "Boost";

    partial void OnLoadPercentChanged(double value) => OnPropertyChanged(nameof(LoadText));

    /// <summary>True when Smart Boost should trim automatically (settings-backed).</summary>
    public bool SmartBoostEnabled
    {
        get => _settings.Current.SmartBoostEnabled;
        set
        {
            if (_settings.Current.SmartBoostEnabled == value)
            {
                return;
            }

            _settings.Current.SmartBoostEnabled = value;
            _settings.Save();
            OnPropertyChanged();
            StatusMessage = value
                ? $"Smart Boost on - memory is trimmed automatically above {_settings.Current.SmartBoostThresholdPercent}%."
                : "Smart Boost off.";
        }
    }

    public override async Task LoadAsync()
    {
        RefreshMemory();
        await RefreshTempAsync();
        await RefreshOverviewAsync();
    }

    protected override void OnBusyChanged()
    {
        BoostCommand.NotifyCanExecuteChanged();
        CaptureCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(BoostButtonText));
    }

    private void OnTick()
    {
        RefreshMemory();

        if (!_settings.Current.SmartBoostEnabled || IsBusy)
        {
            return;
        }

        if (LoadPercent < _settings.Current.SmartBoostThresholdPercent)
        {
            return;
        }

        // At most one automatic trim per minute so the machine is not constantly poked.
        if (DateTime.UtcNow - _lastSmartBoost < TimeSpan.FromMinutes(1))
        {
            return;
        }

        _lastSmartBoost = DateTime.UtcNow;
        _logger.Info($"Smart Boost triggered at {LoadPercent:0}% load.");
        _ = BoostAsync(smart: true);
    }

    private void RefreshMemory()
    {
        try
        {
            ApplySnapshot(_boost.GetMemorySnapshot());
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task RefreshTempAsync()
    {
        try
        {
            TempFilesText = ByteFormatter.Format(await Task.Run(_tempCleaner.GetReclaimableBytes));
        }
        catch (Exception ex)
        {
            TempFilesText = "unavailable";
            _logger.Error("Temp size scan failed.", ex);
        }
    }

    private void OpenTips()
    {
        try
        {
            _launcher.OpenUri(TipsUrl);
            StatusMessage = "Opened the Windows keyboard-shortcut tips.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not open the tips page: {ex.Message}";
            _logger.Error("Could not open the tips page.", ex);
        }
    }

    /// <summary>Counts the user-facing processes and reads the system drive, exactly as the cards show them.</summary>
    private async Task RefreshOverviewAsync()
    {
        try
        {
            IReadOnlyList<ProcessInfo> processes = await _processes.GetProcessesAsync();
            int count = processes.Count(p =>
                !p.IsSystem && !AppInfo.IsProtectedProcessName(p.Name) && !string.IsNullOrWhiteSpace(p.Name));
            ProcessCountText = count == 1 ? "1 app" : $"{count} apps";
        }
        catch (Exception ex)
        {
            ProcessCountText = "unavailable";
            _logger.Error("Process count failed.", ex);
        }

        try
        {
            IReadOnlyList<DriveSpace> drives = await _storage.GetDrivesAsync();
            string root = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? "C:\\";
            DriveSpace? systemDrive = drives.FirstOrDefault(d =>
                string.Equals(d.Name, root, StringComparison.OrdinalIgnoreCase));
            systemDrive ??= drives.FirstOrDefault();

            if (systemDrive is null || systemDrive.TotalBytes <= 0)
            {
                DiskUsageText = "unavailable";
                return;
            }

            const double gigabyte = 1024d * 1024d * 1024d;
            DiskUsageText = $"{systemDrive.UsedBytes / gigabyte:0.#}/{systemDrive.TotalBytes / gigabyte:0.#} GB";
            DiskUsedPercent = systemDrive.UsedPercent;
        }
        catch (Exception ex)
        {
            DiskUsageText = "unavailable";
            _logger.Error("Drive usage failed.", ex);
        }
    }

    private async Task CaptureAsync()
    {
        IsBusy = true;
        StatusMessage = "Capturing every screen...";

        try
        {
            OperationResult result = await _toolbox.LaunchAsync("screenshot");
            StatusMessage = result.Message;
            if (!result.Success)
            {
                _logger.Error($"Screenshot failed: {result.Message}");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Capture failed: {ex.Message}";
            _logger.Error("Capture failed.", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task BoostAsync() => BoostAsync(smart: false);

    private async Task BoostAsync(bool smart)
    {
        IsBusy = true;
        StatusMessage = smart ? "Smart Boost is trimming memory..." : "Trimming process working sets...";

        try
        {
            MemoryTrimResult trim = await _boost.OptimizeAsync();
            ApplySnapshot(trim.After);

            long tempFreed = await _boost.CleanTempFilesAsync();
            await RefreshTempAsync();

            // Trimming working sets does not free memory; report what the OS actually did.
            ResultText = DescribeBoost(trim, tempFreed);
            BoostStatusText = $"{ByteFormatter.Format(tempFreed)} of temporary files released.";
            StatusMessage = smart ? "Smart Boost finished." : "Boost complete.";
            _logger.Info(
                $"Boost: {trim.WorkingSetsTrimmed} working sets trimmed, available {trim.Before.AvailableBytes} -> {trim.After.AvailableBytes} bytes ({trim.AvailableDeltaBytes}), {tempFreed} bytes of temp removed.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Boost failed: {ex.Message}";
            _logger.Error("Boost failed.", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string DescribeBoost(MemoryTrimResult trim, long tempFreed)
    {
        string trimmed = trim.WorkingSetsTrimmed == 1
            ? "1 working set trimmed"
            : $"{trim.WorkingSetsTrimmed} working sets trimmed";

        string memory =
            $"available memory {ByteFormatter.Format((long)trim.Before.AvailableBytes)} → {ByteFormatter.Format((long)trim.After.AvailableBytes)} ({trim.AvailableDeltaText})";

        return $"{trimmed}; {memory}; {ByteFormatter.Format(tempFreed)} of temporary files removed.";
    }

    private void ApplySnapshot(MemorySnapshot snapshot)
    {
        LoadPercent = snapshot.LoadPercent;
        AvailableText = $"{ByteFormatter.Format((long)snapshot.AvailableBytes)} free of {ByteFormatter.Format((long)snapshot.TotalPhysicalBytes)}";
    }

    public void Dispose() => _timer.Stop();
}
