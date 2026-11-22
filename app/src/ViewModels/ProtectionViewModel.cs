using CommunityToolkit.Mvvm.Input;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.ViewModels;

/// <summary>
/// Protection: the security status banner, the Windows Update check, and the four action
/// cards (pop-up management, default browser, taskbar repair, restore default apps).
/// </summary>
public sealed class ProtectionViewModel : ViewModelBase
{
    private readonly IProtectionService _protection;
    private readonly ISystemRepairService _repair;
    private readonly IDefaultBrowserService _browser;
    private readonly IAppLogger _logger;

    public ProtectionViewModel(
        IProtectionService protection,
        ISystemRepairService repair,
        IDefaultBrowserService browser,
        IAppLogger logger)
    {
        _protection = protection;
        _repair = repair;
        _browser = browser;
        _logger = logger;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
        CheckUpdatesCommand = new AsyncRelayCommand(CheckUpdatesAsync, () => !IsBusy);
        PopupCommand = new RelayCommand(() => Run(_repair.OpenNotificationSettings));
        BrowserCommand = new AsyncRelayCommand(BrowserAsync, () => !IsBusy);
        TaskbarCommand = new AsyncRelayCommand(TaskbarAsync, () => !IsBusy);
        RestoreAppsCommand = new RelayCommand(() => Run(_repair.OpenDefaultAppsSettings));
    }

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand CheckUpdatesCommand { get; }
    public RelayCommand PopupCommand { get; }
    public AsyncRelayCommand BrowserCommand { get; }
    public AsyncRelayCommand TaskbarCommand { get; }
    public RelayCommand RestoreAppsCommand { get; }

    private bool _bannerHealthy = true;
    public bool BannerHealthy
    {
        get => _bannerHealthy;
        private set => SetProperty(ref _bannerHealthy, value);
    }

    private string _bannerTitle = "Checking your protection status...";
    public string BannerTitle
    {
        get => _bannerTitle;
        private set => SetProperty(ref _bannerTitle, value);
    }

    private string _bannerDetail = "Definitions, real-time protection and Windows Update are checked locally.";
    public string BannerDetail
    {
        get => _bannerDetail;
        private set => SetProperty(ref _bannerDetail, value);
    }

    private string _defenderDetail = "Checking...";
    public string DefenderDetail
    {
        get => _defenderDetail;
        private set => SetProperty(ref _defenderDetail, value);
    }

    private string _updateDetail = "Checking...";
    public string UpdateDetail
    {
        get => _updateDetail;
        private set => SetProperty(ref _updateDetail, value);
    }

    private string _browserName = "-";
    public string BrowserName
    {
        get => _browserName;
        private set => SetProperty(ref _browserName, value);
    }

    public override Task LoadAsync() => RefreshAsync();

    protected override void OnBusyChanged()
    {
        RefreshCommand.NotifyCanExecuteChanged();
        CheckUpdatesCommand.NotifyCanExecuteChanged();
        BrowserCommand.NotifyCanExecuteChanged();
        TaskbarCommand.NotifyCanExecuteChanged();
    }

    private async Task RefreshAsync()
    {
        IsBusy = true;

        try
        {
            DefenderStatus defender = await Task.Run(_protection.GetDefenderStatus);
            UpdateStatus update = await Task.Run(_protection.GetUpdateStatus);
            DefaultBrowserSetting browser = await Task.Run(_browser.GetDefaultBrowser);

            DefenderDetail = string.Join(Environment.NewLine, new[]
            {
                defender.StatusText,
                string.IsNullOrWhiteSpace(defender.SignatureVersion) ? null : $"Definitions: {defender.SignatureVersion}"
            }.Where(line => !string.IsNullOrWhiteSpace(line)));

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

        try
        {
            UpdateStatus update = await Task.Run(_protection.GetUpdateStatus);
            UpdateDetail = update.StatusText;

            // Opening the page is the supported way to actually start a check.
            Run(() => _repair.OpenWindowsUpdateSettings(), announce: false);
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

    private void Run(Func<OperationResult> action, bool announce = true)
    {
        OperationResult result = action();

        if (announce)
        {
            StatusMessage = result.Message;
        }
    }
}
