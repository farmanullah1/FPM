using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCManager.Core;
using PCManager.Core.Formatting;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.ViewModels;

/// <summary>
/// Settings: start with Windows (own Run value), Smart Boost threshold, the Light/Dark
/// switch and the About block. All persistent state lives in
/// %LOCALAPPDATA%\FarmanullahPCManager\settings.json; the Run value is the only registry
/// write and it uses the value name "FarmanullahPCManager".
/// </summary>
public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly IAppStartupService _appStartup;
    private readonly ISettingsService _settings;
    private readonly IThemeService _theme;
    private readonly IToolLauncher _launcher;
    private readonly IWhitelistService _whitelist;
    private readonly IAppLogger _logger;

    public SettingsViewModel(
        IAppStartupService appStartup,
        ISettingsService settings,
        IThemeService theme,
        IToolLauncher launcher,
        IWhitelistService whitelist,
        IAppLogger logger)
    {
        _appStartup = appStartup;
        _settings = settings;
        _theme = theme;
        _launcher = launcher;
        _whitelist = whitelist;
        _logger = logger;

        OpenDataFolderCommand = new RelayCommand(() => OpenFolder(AppInfo.DataFolder));
        OpenLogFolderCommand = new RelayCommand(OpenLogFolder);
        ResetThresholdCommand = new RelayCommand(() => SmartBoostThreshold = 75);
        AddWhitelistCommand = new RelayCommand(AddWhitelistPath);
        RemoveWhitelistCommand = new RelayCommand(RemoveWhitelistPath, () => SelectedWhitelistEntry is not null);
        ReloadWhitelistCommand = new RelayCommand(ReloadWhitelist);
        OpenWhitelistFolderCommand = new RelayCommand(() => OpenFolder(AppInfo.DataFolder));
        SetQuarantineRetentionCommand = new RelayCommand<string>(SetQuarantineRetention);
        SetQuarantineMaxSizeCommand = new RelayCommand<string>(SetQuarantineMaxSize);

        RefreshWhitelist();
    }

    public RelayCommand OpenDataFolderCommand { get; }
    public RelayCommand OpenLogFolderCommand { get; }
    public RelayCommand ResetThresholdCommand { get; }
    public RelayCommand AddWhitelistCommand { get; }
    public RelayCommand RemoveWhitelistCommand { get; }
    public RelayCommand ReloadWhitelistCommand { get; }
    public RelayCommand OpenWhitelistFolderCommand { get; }
    public RelayCommand<string> SetQuarantineRetentionCommand { get; }
    public RelayCommand<string> SetQuarantineMaxSizeCommand { get; }

    public ObservableCollection<string> WhitelistEntries { get; } = new();

    [ObservableProperty]
    private string _whitelistPathInput = string.Empty;

    [ObservableProperty]
    private string? _selectedWhitelistEntry;

    [ObservableProperty]
    private string _whitelistCountText = "No protected paths yet.";

    public string WhitelistFilePath => _whitelist.FilePath;

    partial void OnSelectedWhitelistEntryChanged(string? value)
        => RemoveWhitelistCommand.NotifyCanExecuteChanged();

    public override Task LoadAsync()
    {
        RefreshWhitelist();
        return Task.CompletedTask;
    }

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

    /// <summary>True for the dark palette; the switch writes the same setting the shell uses.</summary>
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

    public string QuarantinePolicyText =>
        $"Keeps quarantined files for {_settings.Current.QuarantineRetentionDays} day(s), at most " +
        $"{ByteFormatter.Format(_settings.Current.QuarantineMaxBytes)}. The policy is applied before a " +
        "quarantined cleanup and when you empty the quarantine from Storage.";

    private void SetQuarantineRetention(string? days)
    {
        if (!int.TryParse(days, out int value))
        {
            return;
        }

        value = Math.Clamp(value, 1, 365);
        _settings.Current.QuarantineRetentionDays = value;
        _settings.Save();
        OnPropertyChanged(nameof(QuarantinePolicyText));
        StatusMessage = $"Quarantine retention set to {value} day(s).";
    }

    private void SetQuarantineMaxSize(string? gigabytes)
    {
        if (!long.TryParse(gigabytes, out long value))
        {
            return;
        }

        value = Math.Clamp(value, 1, 100);
        _settings.Current.QuarantineMaxBytes = value * 1024L * 1024 * 1024;
        _settings.Save();
        OnPropertyChanged(nameof(QuarantinePolicyText));
        StatusMessage = $"Quarantine size limit set to {value} GB.";
    }

    private void AddWhitelistPath()
    {
        string input = Environment.ExpandEnvironmentVariables(WhitelistPathInput.Trim());

        if (string.IsNullOrWhiteSpace(input))
        {
            StatusMessage = "Enter a folder or file to protect.";
            return;
        }

        string full;

        try
        {
            full = Path.GetFullPath(input);
        }
        catch (Exception ex)
        {
            StatusMessage = "That path is not valid: " + ex.Message;
            return;
        }

        if (!Directory.Exists(full) && !File.Exists(full))
        {
            StatusMessage = "Choose an existing folder or file. Nothing was added.";
            return;
        }

        _whitelist.Add(full);
        WhitelistPathInput = string.Empty;
        RefreshWhitelist();
        StatusMessage = "Protected from scanning and deletion: " + full;
        _logger.Info("Whitelist entry added: " + full);
    }

    private void RemoveWhitelistPath()
    {
        if (SelectedWhitelistEntry is not { } entry)
        {
            return;
        }

        bool removed = _whitelist.Remove(entry);
        RefreshWhitelist();
        StatusMessage = removed
            ? "Removed from the whitelist: " + entry
            : "That entry was not found in the whitelist.";
    }

    private void ReloadWhitelist()
    {
        _whitelist.Reload();
        RefreshWhitelist();
        StatusMessage = "Whitelist reloaded from disk.";
    }

    private void RefreshWhitelist()
    {
        WhitelistEntries.Clear();

        foreach (string entry in _whitelist.GetEntries())
        {
            WhitelistEntries.Add(entry);
        }

        WhitelistCountText = WhitelistEntries.Count switch
        {
            0 => "No protected paths yet.",
            1 => "1 protected path. It is never scanned or deleted.",
            _ => $"{WhitelistEntries.Count} protected paths. They are never scanned or deleted."
        };

        SelectedWhitelistEntry = null;
        RemoveWhitelistCommand.NotifyCanExecuteChanged();
    }

    private void OpenLogFolder()
    {
        Directory.CreateDirectory(AppInfo.LogFolder);
        OpenFolder(AppInfo.LogFolder);
    }

    private void OpenFolder(string path)
    {
        try
        {
            _launcher.OpenFolder(path);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }
}
