using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.ViewModels;

/// <summary>
/// Protection: the security status banner, the Windows Update check, the four action
/// cards (pop-up management, default browser, taskbar repair, restore default apps),
/// and the network connectivity diagnostics & virus scan triggers.
/// </summary>
public sealed partial class ProtectionViewModel : ViewModelBase
{
    private readonly IProtectionService _protection;
    private readonly ISystemRepairService _repair;
    private readonly IDefaultBrowserService _browser;
    private readonly IAppLogger _logger;
    private readonly INetworkDiagnosticsService? _networkDiag;

    public ProtectionViewModel(
        IProtectionService protection,
        ISystemRepairService repair,
        IDefaultBrowserService browser,
        IAppLogger logger,
        INetworkDiagnosticsService? networkDiag = null)
    {
        _protection = protection;
        _repair = repair;
        _browser = browser;
        _logger = logger;
        _networkDiag = networkDiag;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
        CheckUpdatesCommand = new AsyncRelayCommand(CheckUpdatesAsync, () => !IsBusy);
        PopupCommand = new RelayCommand(() => Run(_repair.OpenNotificationSettings));
        BrowserCommand = new AsyncRelayCommand(BrowserAsync, () => !IsBusy);
        TaskbarCommand = new AsyncRelayCommand(TaskbarAsync, () => !IsBusy);
        RestoreAppsCommand = new RelayCommand(() => Run(_repair.OpenDefaultAppsSettings));
        RunNetworkDiagnosticsCommand = new AsyncRelayCommand(RunNetworkDiagnosticsAsync, () => !IsBusy && !IsRunningNetworkDiag);
        FlushDnsCommand = new AsyncRelayCommand(FlushDnsAsync, () => !IsBusy);
        StartQuickScanCommand = new AsyncRelayCommand(StartQuickScanAsync, () => !IsBusy);
    }

    public ObservableCollection<PingResultItem> PingResults { get; } = new();

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand CheckUpdatesCommand { get; }
    public RelayCommand PopupCommand { get; }
    public AsyncRelayCommand BrowserCommand { get; }
    public AsyncRelayCommand TaskbarCommand { get; }
    public RelayCommand RestoreAppsCommand { get; }
    public AsyncRelayCommand RunNetworkDiagnosticsCommand { get; }
    public AsyncRelayCommand FlushDnsCommand { get; }
    public AsyncRelayCommand StartQuickScanCommand { get; }

    [ObservableProperty]
    private NetworkAdapterDetails? _activeAdapter;

    [ObservableProperty]
    private bool _hasNetworkDiagResult;

    [ObservableProperty]
    private bool _isRunningNetworkDiag;

    [ObservableProperty]
    private string _networkSummary = "Check your active adapter, DNS response, and latency.";

    [ObservableProperty]
    private bool _bannerHealthy = true;

    [ObservableProperty]
    private string _bannerTitle = "Checking your protection status...";

    [ObservableProperty]
    private string _bannerDetail = "Definitions, real-time protection and Windows Update are checked locally.";

    [ObservableProperty]
    private string _defenderDetail = "Checking...";

    [ObservableProperty]
    private string _updateDetail = "Checking...";

    [ObservableProperty]
    private string _browserName = "-";

    public override Task LoadAsync() => RefreshAsync();

    protected override void OnBusyChanged()
    {
        RefreshCommand.NotifyCanExecuteChanged();
        CheckUpdatesCommand.NotifyCanExecuteChanged();
        BrowserCommand.NotifyCanExecuteChanged();
        TaskbarCommand.NotifyCanExecuteChanged();
        RunNetworkDiagnosticsCommand.NotifyCanExecuteChanged();
        FlushDnsCommand.NotifyCanExecuteChanged();
        StartQuickScanCommand.NotifyCanExecuteChanged();
    }

    private async Task RefreshAsync()
    {
        IsBusy = true;

        try
        {
            DefenderStatus defender = await Task.Run(_protection.GetDefenderStatus);
            UpdateStatus update = await Task.Run(_protection.GetUpdateStatus);
            DefaultBrowserSetting browser = await Task.Run(_browser.GetDefaultBrowser);

            DefenderDetail = string.IsNullOrWhiteSpace(defender.SignatureVersion)
                ? defender.StatusText
                : $"{defender.StatusText}{Environment.NewLine}Definitions: {defender.SignatureVersion}";
            UpdateDetail = update.StatusText;
            BrowserName = string.IsNullOrWhiteSpace(browser.Name) ? "Not detected" : browser.Name;

            bool defenderOk = defender.RealTimeEnabled && defender.AntivirusEnabled;
            bool updateOk = update.UpdateServiceRunning && !update.PendingReboot;

            BannerHealthy = defenderOk && updateOk;
            BannerTitle = BannerHealthy
                ? "You are protected"
                : defenderOk
                    ? "Windows Update needs attention"
                    : "Real-time protection needs attention";

            BannerDetail = string.Join(" · ", new[]
            {
                defenderOk ? "Defender real-time protection on" : "Defender real-time protection off",
                updateOk ? "Windows Update healthy" : update.PendingReboot ? "Restart pending" : "Update service stopped"
            });

            StatusMessage = $"Checked at {DateTime.Now:HH:mm}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not read protection status: {ex.Message}";
            _logger.Error("Protection status failed.", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CheckUpdatesAsync()
    {
        IsBusy = true;
        StatusMessage = "Reading Windows Update state...";

        try
        {
            UpdateStatus update = await Task.Run(_protection.GetUpdateStatus);
            UpdateDetail = update.StatusText;
            _repair.OpenWindowsUpdateSettings();
            StatusMessage = update.PendingReboot
                ? "A restart is required to finish installing updates."
                : update.UpdateServiceRunning
                    ? "Windows Update service is running. Opened Windows Update settings."
                    : "The Windows Update service is stopped.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task BrowserAsync()
    {
        IsBusy = true;

        try
        {
            DefaultBrowserSetting browser = await Task.Run(_browser.GetDefaultBrowser);
            BrowserName = string.IsNullOrWhiteSpace(browser.Name) ? "Not detected" : browser.Name;
            StatusMessage = _browser.OpenDefaultAppsSettings().Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task TaskbarAsync()
    {
        IsBusy = true;
        StatusMessage = "Restarting Explorer...";

        try
        {
            OperationResult result = await Task.Run(_repair.RestartExplorer);
            StatusMessage = result.Message;
            _logger.Info($"Taskbar repair: {result.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunNetworkDiagnosticsAsync()
    {
        if (_networkDiag is null)
        {
            NetworkSummary = "Network diagnostics service is not available.";
            return;
        }

        IsRunningNetworkDiag = true;
        NetworkSummary = "Testing adapter, gateway, DNS, and internet endpoints...";

        try
        {
            NetworkDiagnosticsSummary summary = await _networkDiag.RunDiagnosticsAsync();
            ActiveAdapter = summary.Adapter;
            PingResults.Clear();
            foreach (PingResultItem ping in summary.Pings)
            {
                PingResults.Add(ping);
            }

            HasNetworkDiagResult = true;
            NetworkSummary = summary.OverallStatus;
            StatusMessage = summary.OverallStatus;
        }
        catch (Exception ex)
        {
            NetworkSummary = $"Network diagnostics failed: {ex.Message}";
            _logger.Error("Network diagnostics failed.", ex);
        }
        finally
        {
            IsRunningNetworkDiag = false;
        }
    }

    private async Task FlushDnsAsync()
    {
        if (_networkDiag is null)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "Flushing DNS resolver cache...";

        try
        {
            OperationResult result = await _networkDiag.FlushDnsCacheAsync();
            StatusMessage = result.Message;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Flush DNS failed: {ex.Message}";
            _logger.Error("Flush DNS failed.", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task StartQuickScanAsync()
    {
        if (_networkDiag is null)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "Initiating Windows Defender Quick Scan...";

        try
        {
            OperationResult result = await _networkDiag.StartQuickVirusScanAsync();
            StatusMessage = result.Message;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Quick scan failed: {ex.Message}";
            _logger.Error("Quick scan failed.", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Run(Func<OperationResult> action) => StatusMessage = action().Message;
}
