using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCManager.Core.Formatting;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.ViewModels;

/// <summary>Wraps a cleanup category so the checkbox can write straight back to the model.</summary>
public sealed class CleanupCategoryViewModel : ObservableObject
{
    private readonly Action _changed;

    public CleanupCategoryViewModel(CleanupCategory model, Action changed)
    {
        Model = model;
        _changed = changed;
    }

    public CleanupCategory Model { get; }

    public string Title => Model.Title;
    public string Description => Model.Description;
    public string SizeText => Model.SizeText;

    public bool IsSelected
    {
        get => Model.IsSelected;
        set
        {
            if (Model.IsSelected == value)
            {
                return;
            }

            Model.IsSelected = value;
            OnPropertyChanged();
            _changed();
        }
    }

    /// <summary>Re-reads the measured size and selection after a cleanup run.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(SizeText));
        OnPropertyChanged(nameof(IsSelected));
    }
}

/// <summary>
/// Storage: system-drive usage, deep cleanup with per-category byte counts, the large-file
/// scanner with size filters, duplicate detection and the Storage Sense switch.
/// </summary>
public sealed partial class StorageViewModel : ViewModelBase
{
    private readonly IStorageService _storage;
    private readonly IDeepCleanupService _cleanup;
    private readonly IDuplicateFinderService _duplicates;
    private readonly ISettingsService _settings;
    private readonly IToolLauncher _launcher;
    private readonly IAppLogger _logger;

    private long _largeFileThresholdBytes;

    public StorageViewModel(
        IStorageService storage,
        IDeepCleanupService cleanup,
        IDuplicateFinderService duplicates,
        ISettingsService settings,
        IToolLauncher launcher,
        IAppLogger logger)
    {
        _storage = storage;
        _cleanup = cleanup;
        _duplicates = duplicates;
        _settings = settings;
        _launcher = launcher;
        _logger = logger;

        ScanCleanupCommand = new AsyncRelayCommand(ScanCleanupAsync, () => !IsBusy);
        CleanSelectedCommand = new AsyncRelayCommand(CleanSelectedAsync, () => !IsBusy && CleanupCategories.Any(c => c.IsSelected));
        FilterLargeFilesCommand = new AsyncRelayCommand<string>(FilterLargeFilesAsync, _ => !IsBusy);
        FindDuplicatesCommand = new AsyncRelayCommand(FindDuplicatesAsync, () => !IsBusy);
        OpenScanFolderCommand = new RelayCommand(OpenScanFolder);

        _scanPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _largeFileThresholdBytes = Math.Max(10L * 1024 * 1024, settings.Current.LargeFileThresholdBytes);
    }

    public ObservableCollection<CleanupCategoryViewModel> CleanupCategories { get; } = new();
    public ObservableCollection<LargeFile> LargeFiles { get; } = new();
    public ObservableCollection<DuplicateGroup> DuplicateGroups { get; } = new();

    public AsyncRelayCommand ScanCleanupCommand { get; }
    public AsyncRelayCommand CleanSelectedCommand { get; }
    public AsyncRelayCommand<string> FilterLargeFilesCommand { get; }
    public AsyncRelayCommand FindDuplicatesCommand { get; }
    public RelayCommand OpenScanFolderCommand { get; }

    [ObservableProperty]
    private string _systemDriveName = "C:";

    [ObservableProperty]
    private string _driveSummary = "Reading drive information...";

    [ObservableProperty]
    private double _driveUsedPercent;

    [ObservableProperty]
    private string _driveUsedText = "-";

    [ObservableProperty]
    private string _driveFreeText = "-";

    [ObservableProperty]
    private string _cleanupTotalText = "-";

    [ObservableProperty]
    private string _scanPath;

    [ObservableProperty]
    private string _largeFileSummary = "Choose a size filter to scan for large files.";

    [ObservableProperty]
    private string _duplicateSummary = "Scan for identical files to see what can be removed.";

    [ObservableProperty]
    private bool _storageSenseEnabled;

    private bool _initialisingSense;

    public string LargeFileFilterText => _largeFileThresholdBytes >= 1024L * 1024 * 1024
        ? $"> {_largeFileThresholdBytes / (1024L * 1024 * 1024)} GB"
        : $"> {_largeFileThresholdBytes / (1024 * 1024)} MB";

    partial void OnStorageSenseEnabledChanged(bool value)
    {
        if (!_initialisingSense)
        {
            StatusMessage = _storage.SetStorageSenseEnabled(value).Message;
        }
    }

    public override async Task LoadAsync()
    {
        await RefreshDriveAsync();
        await ScanCleanupAsync();

        _initialisingSense = true;
        StorageSenseEnabled = _storage.GetStorageSenseEnabled();
        _initialisingSense = false;
    }

    protected override void OnBusyChanged()
    {
        ScanCleanupCommand.NotifyCanExecuteChanged();
        CleanSelectedCommand.NotifyCanExecuteChanged();
        FilterLargeFilesCommand.NotifyCanExecuteChanged();
        FindDuplicatesCommand.NotifyCanExecuteChanged();
    }

    private async Task RefreshDriveAsync()
    {
        try
        {
            IReadOnlyList<DriveSpace> drives = await _storage.GetDrivesAsync();
            string systemRoot = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? "C:\\";

            DriveSpace? system = drives.FirstOrDefault(d => string.Equals(d.Name, systemRoot, StringComparison.OrdinalIgnoreCase))
                ?? drives.FirstOrDefault();

            if (system is null)
            {
                DriveSummary = "No fixed drive was reported.";
                return;
            }

            SystemDriveName = system.Name.TrimEnd('\\');
            DriveUsedPercent = system.UsedPercent;
            DriveUsedText = $"{ByteFormatter.Format(system.UsedBytes)} used";
            DriveFreeText = $"{ByteFormatter.Format(system.FreeBytes)} free";
            DriveSummary = $"{system.Format} · {system.SizeSummary}";
        }
        catch (Exception ex)
        {
            DriveSummary = ex.Message;
            _logger.Error("Drive refresh failed.", ex);
        }
    }

    private async Task ScanCleanupAsync()
    {
        IsBusy = true;
        StatusMessage = "Measuring cleanup categories...";

        try
        {
            IReadOnlyList<CleanupCategory> categories = await _cleanup.ScanAsync();

            CleanupCategories.Clear();
            foreach (CleanupCategory category in categories)
            {
                CleanupCategories.Add(new CleanupCategoryViewModel(category, UpdateCleanupTotal));
            }

            UpdateCleanupTotal();
            StatusMessage = $"{CleanupCategories.Count} categories measured.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Cleanup scan failed: {ex.Message}";
            _logger.Error("Cleanup scan failed.", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void UpdateCleanupTotal()
    {
        long total = CleanupCategories.Where(c => c.IsSelected).Sum(c => c.Model.Bytes);
        CleanupTotalText = $"{ByteFormatter.Format(total)} selected";
        CleanSelectedCommand.NotifyCanExecuteChanged();
    }

    private async Task CleanSelectedAsync()
    {
        IsBusy = true;
        StatusMessage = "Cleaning the selected categories...";

        try
        {
            CleanupOutcome outcome = await _cleanup.CleanAsync(CleanupCategories.Select(c => c.Model));

            foreach (CleanupCategoryViewModel category in CleanupCategories)
            {
                category.Refresh();
            }

            UpdateCleanupTotal();
            StatusMessage = outcome.Message;
            _logger.Info($"Deep cleanup: {outcome.Message} (cleaned {outcome.Cleaned}, skipped {outcome.Skipped}).");

            await RefreshDriveAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Cleanup failed: {ex.Message}";
            _logger.Error("Deep cleanup failed.", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task FilterLargeFilesAsync(string? megabytes)
    {
        if (!long.TryParse(megabytes, out long mb) || mb <= 0)
        {
            return;
        }

        _largeFileThresholdBytes = mb * 1024 * 1024;
        OnPropertyChanged(nameof(LargeFileFilterText));

        _settings.Current.LargeFileThresholdBytes = _largeFileThresholdBytes;
        _settings.Save();

        IsBusy = true;
        StatusMessage = $"Scanning {ScanPath} for files larger than {LargeFileFilterText}...";

        try
        {
            IReadOnlyList<LargeFile> files = await _storage.FindLargeFilesAsync(ScanPath, _largeFileThresholdBytes);

            LargeFiles.Clear();
            foreach (LargeFile file in files)
            {
                LargeFiles.Add(file);
            }

            LargeFileSummary = files.Count == 0
                ? $"No files larger than {LargeFileFilterText} were found."
                : $"{files.Count} files · {ByteFormatter.Format(files.Sum(f => f.Bytes))}";
            StatusMessage = "Large-file scan complete.";
        }
        catch (Exception ex)
        {
            LargeFileSummary = $"Scan failed: {ex.Message}";
            _logger.Error("Large file scan failed.", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task FindDuplicatesAsync()
    {
        IsBusy = true;
        StatusMessage = "Hashing files to find duplicates...";

        try
        {
            IReadOnlyList<DuplicateGroup> groups = await _duplicates.FindDuplicatesAsync(ScanPath);

            DuplicateGroups.Clear();
            foreach (DuplicateGroup group in groups)
            {
                DuplicateGroups.Add(group);
            }

            DuplicateSummary = groups.Count == 0
                ? "No duplicate files were found."
                : $"{groups.Count} duplicate sets · {ByteFormatter.Format(groups.Sum(g => g.WasteBytes))} wasted";
            StatusMessage = "Duplicate scan complete.";
        }
        catch (Exception ex)
        {
            DuplicateSummary = $"Duplicate scan failed: {ex.Message}";
            _logger.Error("Duplicate scan failed.", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OpenScanFolder()
    {
        try
        {
            _launcher.OpenFolder(ScanPath);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }
}
