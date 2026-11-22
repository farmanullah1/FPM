using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.Input;
using PCManager.Core;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.ViewModels;

/// <summary>
/// Settings: start with Windows (own Run value), Smart Boost threshold, the Light/Dark
/// switch and the About block. All persistent state lives in
/// %LOCALAPPDATA%\FarmanullahPCManager\settings.json; the Run value is the only registry
/// write and it uses the value name "FarmanullahPCManager".
/// </summary>
public sealed class SettingsViewModel : ViewModelBase
{
    private readonly IAppStartupService _appStartup;
    private readonly ISettingsService _settings;
    private readonly IThemeService _theme;
    private readonly IAppLogger _logger;

    public SettingsViewModel(
        IAppStartupService appStartup,
        ISettingsService settings,
        IThemeService theme,
        IAppLogger logger)
    {
        _appStartup = appStartup;
        _settings = settings;
        _theme = theme;
        _logger = logger;

        OpenDataFolderCommand = new RelayCommand(OpenDataFolder);
        OpenLogFolderCommand = new RelayCommand(OpenLogFolder);
        ResetThresholdCommand = new RelayCommand(() => SmartBoostThreshold = 75);
    }

    public RelayCommand OpenDataFolderCommand { get; }
    public RelayCommand OpenLogFolderCommand { get; }
    public RelayCommand ResetThresholdCommand { get; }

    public string ProductName => AppInfo.ProductName;
    public string VersionText => $"Version {AppInfo.Version}";
    public string DataFolderPath => AppInfo.DataFolder;
    public string LogFilePath => Path.Combine(AppInfo.LogFolder, $"app-{DateTime.Now:yyyyMMdd}.log");
    public string RunValueText => $@"HKCU\{AppInfo.RunKeyPath} → {AppInfo.RunValueName}";

    public bool StartWithWindows
    {
        get => _appStartup.IsEnabled();
        set
        {
            if (StartWithWindows == value)
            {
                return;
            }

            OperationResult result = _appStartup.SetEnabled(value);
            _settings.Current.StartWithWindows = value;
            _settings.Save();

            OnPropertyChanged();
            StatusMessage = result.Message;
            _logger.Info($"Start with Windows set to {value}: {result.Message}");
        }
    }

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
            StatusMessage = value ? "Smart Boost enabled." : "Smart Boost disabled.";
        }
    }

    public double SmartBoostThreshold
    {
        get => _settings.Current.SmartBoostThresholdPercent;
        set
        {
            int threshold = (int)Math.Clamp(Math.Round(value), 50, 95);

            if (_settings.Current.SmartBoostThresholdPercent == threshold)
            {
                return;
            }

            _settings.Current.SmartBoostThresholdPercent = threshold;
            _settings.Save();
            OnPropertyChanged();
            OnPropertyChanged(nameof(SmartBoostThresholdText));
        }
    }

    public string SmartBoostThresholdText => $"Trim memory automatically above {_settings.Current.SmartBoostThresholdPercent}%";

    /// <summary>True for the dark palette; the switch writes the same theme file the shell uses.</summary>
    public bool IsDark
    {
        get => _theme.Current == ThemeOption.Dark;
        set
        {
            ThemeOption option = value ? ThemeOption.Dark : ThemeOption.Light;
            _theme.Apply(option);
            _settings.Current.Theme = option;
            _settings.Save();
            OnPropertyChanged();
            OnPropertyChanged(nameof(ThemeLabel));
        }
    }

    public string ThemeLabel => IsDark ? "Dark" : "Light";

    private void OpenDataFolder() => OpenFolder(AppInfo.DataFolder);

    private void OpenLogFolder()
    {
        Directory.CreateDirectory(AppInfo.LogFolder);
        OpenFolder(AppInfo.LogFolder);
    }

    private void OpenFolder(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }
}
