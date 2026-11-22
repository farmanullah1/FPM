using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCManager.Core.Formatting;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.ViewModels;

/// <summary>
/// Home: the PC Boost hero card (live memory load + temporary-file size), the Boost
/// action, the Smart Boost switch and the four quick-action cards.
/// </summary>
public sealed partial class HomeViewModel : ViewModelBase, IDisposable
{
    private readonly IPcBoostService _boost;
    private readonly ITempFileCleaner _tempCleaner;
    private readonly ISettingsService _settings;
    private readonly INavigationService _navigation;
    private readonly IAppLogger _logger;
    private readonly DispatcherTimer _timer;

    private DateTime _lastSmartBoost = DateTime.MinValue;

    public HomeViewModel(
        IPcBoostService boost,
        ITempFileCleaner tempCleaner,
        ISettingsService settings,
        INavigationService navigation,
        IAppLogger logger)
    {
        _boost = boost;
        _tempCleaner = tempCleaner;
        _settings = settings;
        _navigation = navigation;
        _logger = logger;

        BoostCommand = new AsyncRelayCommand(BoostAsync, () => !IsBusy);
        OpenProtectionCommand = new RelayCommand(() => _navigation.Navigate(PageKeys.Protection));
        OpenProcessesCommand = new RelayCommand(() => _navigation.Navigate(PageKeys.Apps));
        OpenCleanupCommand = new RelayCommand(() => _navigation.Navigate(PageKeys.Storage));
        OpenStartupCommand = new RelayCommand(() => _navigation.Navigate(PageKeys.Apps));

        StatusMessage = "Ready.";

        // Live gauge: GlobalMemoryStatusEx is allocation-free, so a 4 s poll is cheap.
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _timer.Tick += (_, _) => OnTick();
        _timer.Start();
    }

    public AsyncRelayCommand BoostCommand { get; }
    public RelayCommand OpenProtectionCommand { get; }
    public RelayCommand OpenProcessesCommand { get; }
    public RelayCommand OpenCleanupCommand { get; }
    public RelayCommand OpenStartupCommand { get; }

    [ObservableProperty]
    private double _loadPercent;

    [ObservableProperty]
    private string _availableText = "-";

    [ObservableProperty]
    private string _tempFilesText = "-";

    [ObservableProperty]
    private string _resultText = "Nothing has been optimised yet.";

    public string LoadText => $"{LoadPercent:0}%";

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
    }

    protected override void OnBusyChanged() => BoostCommand.NotifyCanExecuteChanged();

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

    private Task BoostAsync() => BoostAsync(smart: false);

    private async Task BoostAsync(bool smart)
    {
        IsBusy = true;
        StatusMessage = smart ? "Smart Boost is trimming memory..." : "Trimming process working sets...";

        try
        {
            MemorySnapshot snapshot = await _boost.OptimizeAsync();
            ApplySnapshot(snapshot);

            long tempFreed = await _boost.CleanTempFilesAsync();
            await RefreshTempAsync();

            ResultText = $"Trimmed {snapshot.WorkingSetsTrimmed} processes · reclaimed {ByteFormatter.Format(snapshot.ReclaimedBytes)} memory · removed {ByteFormatter.Format(tempFreed)} temporary files.";
            StatusMessage = smart ? "Smart Boost finished." : "Boost complete.";
            _logger.Info($"Boost: {snapshot.WorkingSetsTrimmed} working sets, {snapshot.ReclaimedBytes} bytes reclaimed, {tempFreed} bytes of temp removed.");
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

    private void ApplySnapshot(MemorySnapshot snapshot)
    {
        LoadPercent = snapshot.LoadPercent;
        AvailableText = $"{ByteFormatter.Format((long)snapshot.AvailableBytes)} free of {ByteFormatter.Format((long)snapshot.TotalPhysicalBytes)}";
    }

    public void Dispose() => _timer.Stop();
}
