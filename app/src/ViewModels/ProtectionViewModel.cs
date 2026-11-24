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
    private readonly INetworkSpeedTestService? _speedTest;
    private readonly IPopupMonitorService? _popups;
    private readonly IHostsBlockService? _hosts;
    private readonly ISettingsService? _settings;

    public ProtectionViewModel(
        IProtectionService protection,
        ISystemRepairService repair,
        IDefaultBrowserService browser,
        IAppLogger logger,
        INetworkDiagnosticsService? networkDiag = null,
        INetworkSpeedTestService? speedTest = null,
        IPopupMonitorService? popups = null,
        IHostsBlockService? hosts = null,
        ISettingsService? settings = null)
    {
        _protection = protection;
        _repair = repair;
        _browser = browser;
        _logger = logger;
        _networkDiag = networkDiag;
        _speedTest = speedTest;
        _popups = popups;
        _hosts = hosts;
        _settings = settings;

        if (!string.IsNullOrWhiteSpace(settings?.Current.SpeedTestEndpoint))
        {
            _speedTestEndpoint = settings.Current.SpeedTestEndpoint;
        }

        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
        CheckUpdatesCommand = new AsyncRelayCommand(CheckUpdatesAsync, () => !IsBusy);
        PopupCommand = new RelayCommand(() => Run(_repair.OpenNotificationSettings));
        BrowserCommand = new AsyncRelayCommand(BrowserAsync, () => !IsBusy);
        TaskbarCommand = new AsyncRelayCommand(TaskbarAsync, () => !IsBusy);
        RestoreAppsCommand = new RelayCommand(() => Run(_repair.OpenDefaultAppsSettings));
        RunNetworkDiagnosticsCommand = new AsyncRelayCommand(RunNetworkDiagnosticsAsync, () => !IsBusy && !IsRunningNetworkDiag);
        FlushDnsCommand = new AsyncRelayCommand(FlushDnsAsync, () => !IsBusy);
        StartQuickScanCommand = new AsyncRelayCommand(StartQuickScanAsync, () => !IsBusy);
        RunSpeedTestCommand = new AsyncRelayCommand(RunSpeedTestAsync, () => !IsBusy && !IsSpeedTesting);
        ScanPopupsCommand = new AsyncRelayCommand(ScanPopupsAsync, () => !IsBusy && !IsScanningPopups);
        TogglePopupAllowCommand = new AsyncRelayCommand<PopupSourceInfo>(TogglePopupAllowAsync);
        ApplyHostsBlockCommand = new AsyncRelayCommand(ApplyHostsBlockAsync, () => !IsBusy);
        RemoveHostsBlockCommand = new AsyncRelayCommand(RemoveHostsBlockAsync, () => !IsBusy);

        RefreshHostsState();
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
    public AsyncRelayCommand RunSpeedTestCommand { get; }
    public AsyncRelayCommand ScanPopupsCommand { get; }
    public AsyncRelayCommand<PopupSourceInfo> TogglePopupAllowCommand { get; }
    public AsyncRelayCommand ApplyHostsBlockCommand { get; }
    public AsyncRelayCommand RemoveHostsBlockCommand { get; }

    public ObservableCollection<PopupSourceInfo> PopupSources { get; } = new();

    [ObservableProperty]
    private string _speedTestEndpoint = "https://speed.cloudflare.com/__down?bytes=10000000";

    [ObservableProperty]
    private string _speedTestResultText = "Not run yet. The test downloads from the endpoint above.";

    [ObservableProperty]
    private bool _isSpeedTesting;

    [ObservableProperty]
    private bool _isScanningPopups;

    [ObservableProperty]
    private bool _hasPopupResults;

    [ObservableProperty]
    private string _popupSummary = "Scan to list apps that currently own visible windows.";

    [ObservableProperty]
    private string _hostsBlockInput = string.Empty;

    [ObservableProperty]
    private string _hostsBlockStatus = "Checking the hosts file...";

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
        RunSpeedTestCommand.NotifyCanExecuteChanged();
        ScanPopupsCommand.NotifyCanExecuteChanged();
        ApplyHostsBlockCommand.NotifyCanExecuteChanged();
        RemoveHostsBlockCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsSpeedTestingChanged(bool value) => RunSpeedTestCommand.NotifyCanExecuteChanged();

    partial void OnIsScanningPopupsChanged(bool value) => ScanPopupsCommand.NotifyCanExecuteChanged();

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

    private async Task RunSpeedTestAsync()
    {
        if (_speedTest is null)
        {
            SpeedTestResultText = "The speed-test service is not available.";
            return;
        }

        IsSpeedTesting = true;
        SpeedTestResultText = "Downloading from the endpoint...";
        SaveSpeedTestEndpoint();

        try
        {
            SpeedTestResult result = await _speedTest.RunAsync(SpeedTestEndpoint);
            SpeedTestResultText = result.Message;
            StatusMessage = result.Success ? "Speed test finished." : "Speed test failed.";
        }
        catch (Exception ex)
        {
            SpeedTestResultText = $"Speed test failed: {ex.Message}";
            _logger.Error("Speed test failed.", ex);
        }
        finally
        {
            IsSpeedTesting = false;
        }
    }

    private async Task ScanPopupsAsync()
    {
        if (_popups is null)
        {
            PopupSummary = "The popup monitor is not available.";
            return;
        }

        IsScanningPopups = true;
        PopupSummary = "Looking for visible windows from other apps...";

        try
        {
            IReadOnlyList<PopupSourceInfo> sources = await _popups.ScanAsync();
            PopupSources.Clear();

            foreach (PopupSourceInfo source in sources)
            {
                PopupSources.Add(source);
            }

            HasPopupResults = true;
            PopupSummary = sources.Count == 0
                ? "No other app currently owns a visible top-level window."
                : $"{sources.Count} app(s) own visible windows. Allowed apps stay in the list but are ignored; select Allowed again to revoke.";
        }
        catch (Exception ex)
        {
            PopupSummary = $"Popup scan failed: {ex.Message}";
            _logger.Error("Popup scan failed.", ex);
        }
        finally
        {
            IsScanningPopups = false;
        }
    }

    private void SaveSpeedTestEndpoint()
    {
        if (_settings is null)
        {
            return;
        }

        try
        {
            _settings.Current.SpeedTestEndpoint = SpeedTestEndpoint.Trim();
            _settings.Save();
        }
        catch (Exception ex)
        {
            _logger.Error("Could not save the speed-test endpoint.", ex);
        }
    }

    private async Task TogglePopupAllowAsync(PopupSourceInfo? source)
    {
        if (_popups is null || source is null)
        {
            return;
        }

        bool allow = !source.IsAllowed;
        await _popups.SetAllowedAsync(source.ProcessName, allow);
        StatusMessage = allow
            ? $"{source.ProcessName} will be ignored on the next scan."
            : $"{source.ProcessName} is no longer allowed and will be listed again.";
        await ScanPopupsAsync();
    }

    private async Task ApplyHostsBlockAsync()
    {
        if (_hosts is null)
        {
            HostsBlockStatus = "The hosts block service is not available.";
            return;
        }

        IsBusy = true;
        HostsBlockStatus = "Waiting for administrator approval...";

        try
        {
            OperationResult result = await _hosts.ApplyAsync(HostsBlockInput);
            HostsBlockStatus = result.Message;
            StatusMessage = result.Success ? "Hosts block updated." : result.Message;
            RefreshHostsState();
        }
        catch (Exception ex)
        {
            HostsBlockStatus = $"Hosts block failed: {ex.Message}";
            _logger.Error("Hosts block failed.", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RemoveHostsBlockAsync()
    {
        if (_hosts is null)
        {
            return;
        }

        IsBusy = true;
        HostsBlockStatus = "Waiting for administrator approval...";

        try
        {
            OperationResult result = await _hosts.RemoveAsync();
            HostsBlockStatus = result.Message;
            StatusMessage = result.Message;
            RefreshHostsState();
        }
        catch (Exception ex)
        {
            HostsBlockStatus = $"Removing the hosts block failed: {ex.Message}";
            _logger.Error("Removing the hosts block failed.", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RefreshHostsState()
    {
        HostsBlockStatus = _hosts?.GetState().Detail ?? "Hosts block service is not available.";
    }

    private void Run(Func<OperationResult> action) => StatusMessage = action().Message;
}
