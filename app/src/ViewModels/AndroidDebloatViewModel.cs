using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCManager.Core.Android;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;
using PCManager.Services.Android;

namespace PCManager.ViewModels;

/// <summary>
/// A1 onboarding surface inside Debloat → Android. It locates/verifies adb, gates the pinned
/// download behind explicit consent, shows typed device failures and reads device identity only.
/// The 2 s poll runs only while this tab is visible.
/// </summary>
public sealed partial class AndroidDebloatViewModel : ObservableObject, IDisposable
{
    private readonly IAdbService _adb;
    private readonly ISettingsService _settings;
    private readonly DispatcherTimer _timer;
    private bool _visible;
    private bool _applying;

    public AndroidDebloatViewModel(
        IAdbService adb,
        ISettingsService settings,
        PackageListViewModel packageList)
    {
        _adb = adb;
        _settings = settings;
        PackageList = packageList;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += OnTick;

        AdbPathOverride = settings.Current.AdbPath;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsRefreshing);
        UsePathCommand = new AsyncRelayCommand(UsePathAsync, () => !IsRefreshing);
        DownloadCommand = new AsyncRelayCommand(DownloadAsync, () => ConsentGiven && !IsRefreshing);
    }

    public ObservableCollection<AdbDevice> Devices { get; } = new();
    public ObservableCollection<string> UserRows { get; } = new();
    public PackageListViewModel PackageList { get; }

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand UsePathCommand { get; }
    public AsyncRelayCommand DownloadCommand { get; }

    public string VersionText => $"Google platform-tools {AdbPackage.Version} · SHA-256 {AdbPackage.Sha256[..12]}…";
    public string LicenseText => AdbPackage.LicenseNotice;
    public string LicenseUrl => AdbPackage.LicenseUrl;

    [ObservableProperty]
    private string _adbPathOverride = string.Empty;

    [ObservableProperty]
    private string _adbStatusText = "ADB status: not checked.";

    [ObservableProperty]
    private string _adbPathText = "ADB: not located";

    [ObservableProperty]
    private string _onboardingText = AdbOnboarding.For(AdbStatusKind.Missing);

    [ObservableProperty]
    private string _deviceInfoText = "No Android device selected.";

    [ObservableProperty]
    private string _profileText = "Profiles: not read.";

    [ObservableProperty]
    private string _deviceCountText = "No device listed";

    [ObservableProperty]
    private bool _consentGiven;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private AdbDevice? _selectedDevice;

    partial void OnConsentGivenChanged(bool value) => RefreshCommands();

    partial void OnIsRefreshingChanged(bool value) => RefreshCommands();

    partial void OnSelectedDeviceChanged(AdbDevice? value)
    {
        if (_applying)
        {
            return;
        }

        if (value is null)
        {
            DeviceInfoText = "No Android device selected.";
            return;
        }

        _ = LoadDeviceAsync(value);
    }

    public void SetVisible(bool visible)
    {
        if (_visible == visible)
        {
            return;
        }

        _visible = visible;

        if (visible)
        {
            _timer.Start();
            _ = RefreshAsync();
            _ = PackageList.LoadAsync();
        }
        else
        {
            _timer.Stop();
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
    }

    private async Task RefreshAsync()
    {
        if (IsRefreshing || !_visible)
        {
            return;
        }

        IsRefreshing = true;
        AdbStatusText = "ADB status: checking…";

        try
        {
            Apply(await _adb.RefreshAsync());
        }
        catch (Exception ex)
        {
            AdbStatusText = "ADB status: failed — " + ex.Message;
            OnboardingText = AdbOnboarding.For(AdbStatusKind.ProcessFailed);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private async Task UsePathAsync()
    {
        _settings.Current.AdbPath = AdbPathOverride.Trim();
        _settings.Save();

        IsRefreshing = true;

        try
        {
            AdbLocateResult locate = await _adb.LocateAsync();

            if (locate.Kind != AdbStatusKind.Ready)
            {
                AdbStatusText = "ADB status: " + locate.Kind;
                AdbPathText = locate.ExecutablePath is null
                    ? "ADB: not located"
                    : "ADB: " + locate.ExecutablePath;
                OnboardingText = AdbOnboarding.For(locate.Kind);
                return;
            }

            Apply(await _adb.RefreshAsync());
        }
        catch (Exception ex)
        {
            AdbStatusText = "ADB status: failed — " + ex.Message;
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private async Task DownloadAsync()
    {
        if (!ConsentGiven)
        {
            OnboardingText = AdbOnboarding.For(AdbStatusKind.Declined);
            return;
        }

        IsRefreshing = true;
        IsDownloading = true;
        AdbStatusText = "ADB status: downloading Google platform-tools…";

        try
        {
            Apply(await _adb.InstallPlatformToolsAsync(consented: true));
        }
        catch (Exception ex)
        {
            AdbStatusText = "ADB status: download failed — " + ex.Message;
            OnboardingText = AdbOnboarding.For(AdbStatusKind.DownloadFailed);
        }
        finally
        {
            IsDownloading = false;
            IsRefreshing = false;
        }
    }

    private void Apply(AdbSnapshot snapshot)
    {
        AdbStatusText = $"ADB status: {snapshot.Kind} — {snapshot.Message}";
        AdbPathText = snapshot.ExecutablePath is null
            ? "ADB: not located"
            : "ADB: " + snapshot.ExecutablePath;
        OnboardingText = AdbOnboarding.For(snapshot.Kind);

        _applying = true;
        Devices.Clear();
        foreach (AdbDevice device in snapshot.Devices)
        {
            Devices.Add(device);
        }

        DeviceCountText = snapshot.Devices.Count == 0
            ? "No device listed"
            : $"{snapshot.Devices.Count} device(s) · {snapshot.Devices.Count(device => device.IsUsable)} authorized";

        SelectedDevice = snapshot.Devices.FirstOrDefault(device => device.IsUsable)
            ?? snapshot.Devices.FirstOrDefault();
        _applying = false;

        if (SelectedDevice is null)
        {
            DeviceInfoText = "No Android device selected.";
            ProfileText = "Profiles: not read.";
        }
        else
        {
            _ = LoadDeviceAsync(SelectedDevice);
        }
    }

    private async Task LoadDeviceAsync(AdbDevice device)
    {
        if (!device.IsUsable)
        {
            DeviceInfoText = $"{device.DisplayName} · {device.StateText}. Authorize the phone to read device info.";
            ProfileText = "Profiles: not read.";
            return;
        }

        DeviceInfoText = "Reading device identity…";
        ProfileText = "Profiles: reading…";

        try
        {
            AdbDeviceInfo? info = await _adb.GetDeviceInfoAsync(device);
            IReadOnlyList<AdbUser> users = await _adb.GetUsersAsync(device);

            DeviceInfoText = info is null
                ? "Device identity is unavailable; refresh after authorizing the phone."
                : $"{info.DisplayName} · {info.Brand}\n" +
                  $"Android {info.AndroidVersion} (SDK {info.SdkLevel}) · {info.Transport}\n" +
                  $"Serial {info.Serial} · patch {info.SecurityPatch} · build {info.Incremental}";

            UserRows.Clear();
            foreach (AdbUser user in users)
            {
                UserRows.Add(
                    $"{(user.IsWorkProfile ? "Work profile" : "User")} · id {user.Id} · {user.Name}" +
                    $" · flags {user.Flags}{(user.IsRunning ? " · running" : string.Empty)}");
            }

            ProfileText = users.Count == 0
                ? "Profiles: none reported."
                : $"{users.Count} profile(s) · {users.Count(user => user.IsWorkProfile)} work profile(s)";
        }
        catch (Exception ex)
        {
            DeviceInfoText = "Device identity failed: " + ex.Message;
            ProfileText = "Profiles: not read.";
        }
    }

    private async void OnTick(object? sender, EventArgs e)
    {
        if (!IsRefreshing)
        {
            await RefreshAsync();
        }
    }

    private void RefreshCommands()
    {
        RefreshCommand.NotifyCanExecuteChanged();
        UsePathCommand.NotifyCanExecuteChanged();
        DownloadCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(VersionText));
    }
}
