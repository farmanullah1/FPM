using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.Input;
using PCManager.Core.Formatting;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.ViewModels;

/// <summary>Wraps a cleanup category so the checkbox can write straight back to the model.</summary>
public sealed class CleanupCategoryViewModel : ViewModelBaseItem
{
    private readonly Action _changed;

    public CleanupCategoryViewModel(CleanupCategory category, Action changed) : base(category)
    {
        _changed = changed;
    }

    public CleanupCategory Model => (CleanupCategory)Inner;

    public string Title => Model.Title;
    public string Description => Model.Description;
    public string SizeText => Model.SizeText;
    public bool IsCleaned => Model.IsCleaned;

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

    public void Refresh()
    {
        OnPropertyChanged(nameof(SizeText));
        OnPropertyChanged(nameof(IsSelected));
        OnPropertyChanged(nameof(IsCleaned));
    }
}

/// <summary>Base for view models that wrap one model instance.</summary>
public abstract class ViewModelBaseItem : System.ComponentModel.INotifyPropertyChanged
{
    protected ViewModelBaseItem(object inner) => Inner = inner;

    protected object Inner { get; }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
}

/// <summary>
/// Storage: system-drive usage, deep cleanup with per-category byte counts, the large-file
/// scanner with size filters, duplicate detection and the Storage Sense switch.
/// </summary>
public sealed class StorageViewModel : ViewModelBase
{
    private readonly IStorageService _storage;
    private readonly IDeepCleanupService _cleanup;
    private readonly IDuplicateFinderService _duplicates;
    private readonly ISettingsService _settings;
    private readonly IAppLogger _logger;

    private long _largeFileThresholdBytes = 100L * 1024 * 1024;

    public StorageViewModel(
        IStorageService storage,
        IDeepCleanupService cleanup,
        IDuplicateFinderService duplicates,
        ISettingsService settings,
        IAppLogger logger)
    {
        _storage = storage;
        _cleanup = cleanup;
        _duplicates = duplicates;
        _settings = settings;
        _logger = logger;

        ScanCleanupCommand = new AsyncRelayCommand(ScanCleanupAsync, () => !IsBusy);
        CleanSelectedCommand = new AsyncRelayCommand(CleanSelectedAsync, () => !IsBusy && CleanupCategories.Any(c => c.IsSelected));
        FilterLargeFilesCommand = new AsyncRelayCommand<string>(FilterLargeFilesAsync, _ => !IsBusy);
        FindDuplicatesCommand = new AsyncRelayCommand(FindDuplicatesAsync, () => !IsBusy);
        OpenScanFolderCommand = new RelayCommand(OpenScanFolder);

        _scanPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _largeFileFilterText = "> 100 MB";

        // Remember the last size filter across sessions.
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

    // ---------------------------------------------------------------- drive
    private string _systemDriveName = "C:";
    public string SystemDriveName
    {
        get => _systemDriveName;
        private set => SetProperty(ref _systemDriveName, value);
    }

    private string _driveSummary = "Reading drive information...";
    public string DriveSummary
    {
        get => _driveSummary;
        private set => SetProperty(ref _driveSummary, value);
    }

    private double _driveUsedPercent;
    public double DriveUsedPercent
    {
        get => _driveUsedPercent;
        private set => SetProperty(ref _driveUsedPercent, value);
    }

    private string _driveUsedText = "-";
    public string DriveUsedText
    {
        get => _driveUsedText;
        private set => SetProperty(ref _driveUsedText, value);
    }

    private string _driveFreeText = "-";
    public string DriveFreeText
    {
        get => _driveFreeText;
        private set => SetProperty(ref _driveFreeText, value);
    }

    // ---------------------------------------------------------------- cleanup
    private string _cleanupTotalText = "-";
    public string CleanupTotalText
    {
        get => _cleanupTotalText;
        private set => SetProperty(ref _cleanupTotalText, value);
    }

    // ---------------------------------------------------------------- large files
    private string _scanPath;
    public string ScanPath
    {
        get => _scanPath;
        set => SetProperty(ref _scanPath, value);
    }

    private string _largeFileFilterText;
    public string LargeFileFilterText
    {
        get => _largeFileFilterText;
        private set => SetProperty(ref _largeFileFilterText, value);
    }

    private string _largeFileSummary = "Choose a size filter to scan for large files.";
    public string LargeFileSummary
    {
        get => _largeFileSummary;
        private set => SetProperty(ref _largeFileSummary, value);
    }

    // ---------------------------------------------------------------- duplicates
    private string _duplicateSummary = "Scan for identical files to see what can be removed.";
    public string DuplicateSummary
    {
        get => _duplicateSummary;
        private set => SetProperty(ref _duplicateSummary, value);
    }

    // ---------------------------------------------------------------- storage sense
    private bool _storageSenseEnabled;
    private bool _initialisingSense;

    public bool StorageSenseEnabled
    {
        get => _storageSenseEnabled;
        set
        {
            if (!SetProperty(ref _storageSenseEnabled, value) || _initialisingSense)
            {
                return;
            }

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
            DriveSummary = $"{system.Format} · {system.TotalText} total · {system.SizeSummary}";
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
        LargeFileFilterText = mb >= 1024 ? $"> {mb / 1024} GB" : $"> {mb} MB";

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
            if (Directory.Exists(ScanPath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{ScanPath}\"") { UseShellExecute = true });
            }
            else
            {
                StatusMessage = "That folder does not exist.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }
}
