using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCManager.Core.Elevation;
using PCManager.Core.Formatting;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.ViewModels;

/// <summary>One measured row in the cleanup review table.</summary>
public sealed class CleanupItemViewModel : ObservableObject
{
    private readonly Action _changed;

    public CleanupItemViewModel(CleanupCandidate model, Action changed)
    {
        Model = model;
        _changed = changed;
    }

    public CleanupCandidate Model { get; }

    public int Number => Model.Number;
    public string NumberText => $"#{Model.Number}";
    public string Title => Model.Title;
    public string Category => Model.Category;
    public string Application => Model.Application;
    public string PathText => Model.PathText;
    public string SizeText => Model.SizeText;
    public string DetailText => Model.DetailText;
    public string SafetyText => Model.SafetyText;
    public string RebuildText => Model.RebuildText;
    public string TierText => Model.TierText;
    public CleanupTier Tier => Model.Tier;
    public bool IsOptIn => Model.IsOptIn;
    public string ConfidenceText => Model.ConfidenceText;

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
}

/// <summary>
/// Storage: drive usage, the two-phase Cleanup Center, large files, duplicate detection and
/// Storage Sense.
///
/// The Cleanup Center never deletes from a scan. A scan only measures; an approval command
/// produces a plan; confirmation executes that plan through the safety-guarded executor.
/// </summary>
public sealed partial class StorageViewModel : ViewModelBase
{
    private readonly IStorageService _storage;
    private readonly ICleanupScanner _cleanupScanner;
    private readonly ICleanupCommandParser _cleanupParser;
    private readonly ICleanupExecutor _cleanupExecutor;
    private readonly ICleanupLog _cleanupLog;
    private readonly IRestorePointService _restorePoint;
    private readonly ICleanupHistoryService _history;
    private readonly ICleanupReportExporter _exporter;
    private readonly IDuplicateFinderService _duplicates;
    private readonly ISettingsService _settings;
    private readonly IToolLauncher _launcher;
    private readonly IAppLogger _logger;

    private long _largeFileThresholdBytes;
    private CleanupScanResult? _scan;
    private CleanupPlan? _pendingPlan;
    private CleanupReport? _lastReport;

    public StorageViewModel(
        IStorageService storage,
        ICleanupScanner cleanupScanner,
        ICleanupCommandParser cleanupParser,
        ICleanupExecutor cleanupExecutor,
        ICleanupLog cleanupLog,
        IRestorePointService restorePoint,
        ICleanupHistoryService history,
        ICleanupReportExporter exporter,
        IDuplicateFinderService duplicates,
        ISettingsService settings,
        IToolLauncher launcher,
        IAppLogger logger)
    {
        _storage = storage;
        _cleanupScanner = cleanupScanner;
        _cleanupParser = cleanupParser;
        _cleanupExecutor = cleanupExecutor;
        _cleanupLog = cleanupLog;
        _restorePoint = restorePoint;
        _history = history;
        _exporter = exporter;
        _duplicates = duplicates;
        _settings = settings;
        _launcher = launcher;
        _logger = logger;

        ScanCleanupCommand = new AsyncRelayCommand(ScanCleanupAsync, () => !IsBusy);
        ReviewSelectedCommand = new AsyncRelayCommand(ReviewSelectedAsync, () => !IsBusy && CleanupItems.Count > 0);
        ReviewApprovalCommand = new AsyncRelayCommand(ReviewApprovalAsync, () => !IsBusy && CleanupItems.Count > 0);
        ApplyProfileCommand = new RelayCommand<string>(ApplyProfile, _ => !IsBusy && CleanupItems.Count > 0);
        ExecutePlanCommand = new AsyncRelayCommand(ExecutePlanAsync, () => !IsBusy && HasPendingPlan);
        CancelPlanCommand = new RelayCommand(CancelPlan, () => HasPendingPlan);
        OpenCleanupLogCommand = new RelayCommand(OpenCleanupLogFolder);
        ExportMarkdownCommand = new RelayCommand(ExportMarkdown, () => HasCleanupReport);
        ExportCsvCommand = new RelayCommand(ExportCsv, () => HasCleanupReport);
        OpenReportsCommand = new RelayCommand(OpenReportsFolder);
        FilterLargeFilesCommand = new AsyncRelayCommand<string>(FilterLargeFilesAsync, _ => !IsBusy);
        FindDuplicatesCommand = new AsyncRelayCommand(FindDuplicatesAsync, () => !IsBusy);
        OpenScanFolderCommand = new RelayCommand(OpenScanFolder);

        _scanPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _largeFileThresholdBytes = Math.Max(10L * 1024 * 1024, settings.Current.LargeFileThresholdBytes);
    }

    public ObservableCollection<CleanupItemViewModel> CleanupItems { get; } = new();
    public ObservableCollection<LargeFile> LargeFiles { get; } = new();
    public ObservableCollection<DuplicateGroup> DuplicateGroups { get; } = new();

    public AsyncRelayCommand ScanCleanupCommand { get; }
    public AsyncRelayCommand ReviewSelectedCommand { get; }
    public AsyncRelayCommand ReviewApprovalCommand { get; }
    public RelayCommand<string> ApplyProfileCommand { get; }
    public AsyncRelayCommand ExecutePlanCommand { get; }

    public IReadOnlyList<CleanupProfile> CleanupProfiles => CleanupProfileCatalog.All;
    public RelayCommand CancelPlanCommand { get; }
    public RelayCommand OpenCleanupLogCommand { get; }
    public RelayCommand ExportMarkdownCommand { get; }
    public RelayCommand ExportCsvCommand { get; }
    public RelayCommand OpenReportsCommand { get; }
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
    private string _cleanupTotalText = "Scan to measure cleanup candidates.";

    [ObservableProperty]
    private string _cleanupSummary = "Nothing has been measured yet.";

    [ObservableProperty]
    private string _cleanupNotesText = string.Empty;

    [ObservableProperty]
    private string _approvalCommand = string.Empty;

    [ObservableProperty]
    private string _approvalReviewText = string.Empty;

    [ObservableProperty]
    private bool _hasPendingPlan;

    [ObservableProperty]
    private bool _isDryRun;

    [ObservableProperty]
    private bool _createRestorePoint = true;

    [ObservableProperty]
    private string _cleanupReportText = string.Empty;

    [ObservableProperty]
    private bool _hasCleanupReport;

    [ObservableProperty]
    private string _cleanupHistoryText = "No cleanup runs recorded yet.";

    [ObservableProperty]
    private bool _hasCleanupHistory;

    [ObservableProperty]
    private string _scanPath;

    [ObservableProperty]
    private string _largeFileSummary = "Choose a size filter to scan for large files.";

    [ObservableProperty]
    private string _duplicateSummary = "Scan for identical files to see what can be removed.";

    [ObservableProperty]
    private bool _storageSenseEnabled;

    private bool _initialisingSense;

    public string ApprovalHint =>
        "Type \"proceed\" (every ticked row), \"only 1, 2\" or \"keep 1, 2\". " +
        "Ranges work too: \"only 3-7\". Nothing is deleted until you confirm.";

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

    partial void OnHasPendingPlanChanged(bool value)
    {
        ExecutePlanCommand.NotifyCanExecuteChanged();
        CancelPlanCommand.NotifyCanExecuteChanged();
    }

    partial void OnHasCleanupReportChanged(bool value)
    {
        ExportMarkdownCommand.NotifyCanExecuteChanged();
        ExportCsvCommand.NotifyCanExecuteChanged();
    }

    public override async Task LoadAsync()
    {
        RefreshHistory();
        await RefreshDriveAsync();
        await ScanCleanupCoreAsync();

        _initialisingSense = true;
        StorageSenseEnabled = _storage.GetStorageSenseEnabled();
        _initialisingSense = false;
    }

    protected override void OnBusyChanged()
    {
        ScanCleanupCommand.NotifyCanExecuteChanged();
        ReviewSelectedCommand.NotifyCanExecuteChanged();
        ReviewApprovalCommand.NotifyCanExecuteChanged();
        ApplyProfileCommand.NotifyCanExecuteChanged();
        ExecutePlanCommand.NotifyCanExecuteChanged();
        CancelPlanCommand.NotifyCanExecuteChanged();
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

    private Task ScanCleanupAsync() => ScanCleanupCoreAsync();

    private async Task ScanCleanupCoreAsync()
    {
        IsBusy = true;
        StatusMessage = "Scanning cleanup targets (read-only, nothing is deleted)...";

        try
        {
            CleanupScanResult result = await _cleanupScanner.ScanAsync();

            _scan = result;
            _pendingPlan = null;
            HasPendingPlan = false;
            ApprovalReviewText = string.Empty;

            CleanupItems.Clear();
            foreach (CleanupCandidate candidate in result.Items)
            {
                CleanupItems.Add(new CleanupItemViewModel(candidate, OnCleanupSelectionChanged));
            }

            UpdateCleanupTotal();

            CleanupSummary =
                $"{result.Items.Count} rows measured at {result.ScannedUtc.ToLocalTime():HH:mm:ss}. " +
                "Safe rows are ticked; Caution and Opt-in rows are not.";
            CleanupNotesText = string.Join(" ", result.Notes);

            StatusMessage = $"{result.Items.Count} cleanup targets measured. Nothing was deleted.";

            ReviewSelectedCommand.NotifyCanExecuteChanged();
            ReviewApprovalCommand.NotifyCanExecuteChanged();
            ApplyProfileCommand.NotifyCanExecuteChanged();
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "The cleanup scan was cancelled.";
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

    private void OnCleanupSelectionChanged()
    {
        UpdateCleanupTotal();

        if (!HasPendingPlan)
        {
            return;
        }

        // The approval described the old ticked rows. A changed selection cannot reuse it.
        _pendingPlan = null;
        HasPendingPlan = false;
        ApprovalReviewText = "Selection changed. Review the command again to create a new plan.";
        StatusMessage = "Selection changed; the previous approval was discarded.";
    }

    private void UpdateCleanupTotal()
    {
        if (_scan is null)
        {
            return;
        }

        int selected = CleanupItems.Count(item => item.IsSelected && item.Model.Bytes > 0);
        long total = CleanupItems.Where(item => item.IsSelected).Sum(item => item.Model.Bytes);

        CleanupTotalText =
            $"Potential storage recovery: {ByteFormatter.Format(total)} across {selected} selected row(s). " +
            "This is an estimate until a cleanup is verified.";
    }

    private void ApplyProfile(string? id)
    {
        CleanupProfile? profile = CleanupProfileCatalog.All.FirstOrDefault(candidate =>
            candidate.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

        if (profile is null)
        {
            return;
        }

        int ticked = 0;

        foreach (CleanupItemViewModel item in CleanupItems)
        {
            item.IsSelected = CleanupProfileCatalog.Includes(profile.Kind, item.Model);

            if (item.IsSelected)
            {
                ticked++;
            }
        }

        UpdateCleanupTotal();
        StatusMessage = ticked == 0
            ? $"Profile \"{profile.Name}\" matched no measured rows. Nothing is ticked."
            : $"Profile \"{profile.Name}\" ticked {ticked} row(s). Review them; nothing is deleted until you confirm an approval.";
    }

    private Task ReviewSelectedAsync()
    {
        ApprovalCommand = "proceed";
        return ReviewApprovalAsync();
    }

    private Task ReviewApprovalAsync()
    {
        if (_scan is null)
        {
            ApprovalReviewText = "Scan first.";
            return Task.CompletedTask;
        }

        try
        {
            CleanupCommandResult result = _cleanupParser.Parse(ApprovalCommand, _scan);

            if (result.Kind == CleanupCommandKind.Clarification || result.Plan is null)
            {
                _pendingPlan = null;
                HasPendingPlan = false;
                ApprovalReviewText = result.Message;
                return Task.CompletedTask;
            }

            _pendingPlan = result.Plan;
            HasPendingPlan = true;
            CreateRestorePoint = result.Plan.Items.Any(item =>
                item.Tier is CleanupTier.Caution or CleanupTier.OptIn);

            int optIn = result.Plan.Items.Count(item => item.Tier == CleanupTier.OptIn);

            ApprovalReviewText = result.Message +
                                 (optIn > 0 ? $" Includes {optIn} opt-in row(s)." : string.Empty) +
                                 " Confirm below to run the plan.";

            StatusMessage = "Plan ready. Review the count, then confirm.";
        }
        catch (Exception ex)
        {
            HasPendingPlan = false;
            ApprovalReviewText = $"Could not read that command: {ex.Message}";
            _logger.Error("Cleanup command parsing failed.", ex);
        }

        return Task.CompletedTask;
    }

    private async Task ExecutePlanAsync()
    {
        if (_pendingPlan is not { } plan)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = IsDryRun
            ? "Dry run: logging what would be removed..."
            : "Executing the approved plan...";

        CleanupReport? report = null;
        string restoreNote = string.Empty;

        if (!IsDryRun && CreateRestorePoint)
        {
            StatusMessage = "Creating a System Restore point (administrator prompt may appear)...";

            try
            {
                ElevationOutcome restore = await _restorePoint.CreateAsync();
                restoreNote = restore.Success
                    ? "Restore point: " + restore.Message
                    : "Restore point not created: " + restore.Message;

                _logger.Info(restoreNote);
            }
            catch (Exception ex)
            {
                restoreNote = "Restore point failed: " + ex.Message;
                _logger.Error("Restore point creation failed.", ex);
            }
        }

        try
        {
            report = await _cleanupExecutor.ExecuteAsync(plan, IsDryRun);
            _lastReport = report;
            CleanupReportText = BuildReport(report, restoreNote);
            HasCleanupReport = true;
            StatusMessage = report.Summary;
            _logger.Info($"Cleanup plan {plan.PlanId}: {report.Summary}");

            if (!report.Expired)
            {
                _history.Add(report);
                RefreshHistory();
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Cleanup was cancelled. Files already removed stay removed; the log records them.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Cleanup failed: {ex.Message}";
            _logger.Error("Cleanup execution failed.", ex);
        }
        finally
        {
            IsBusy = false;
        }

        _pendingPlan = null;
        HasPendingPlan = false;
        ApprovalCommand = string.Empty;
        ApprovalReviewText = string.Empty;
        ExportMarkdownCommand.NotifyCanExecuteChanged();
        ExportCsvCommand.NotifyCanExecuteChanged();

        if (report is not null && !report.Expired)
        {
            await ScanCleanupCoreAsync();
        }
    }

    private void RefreshHistory()
    {
        try
        {
            IReadOnlyList<CleanupHistoryEntry> entries = _history.GetRecent(5);
            HasCleanupHistory = entries.Count > 0;
            CleanupHistoryText = entries.Count == 0
                ? "No cleanup runs recorded yet."
                : string.Join(Environment.NewLine, entries.Select(entry => entry.DisplayText));
        }
        catch (Exception ex)
        {
            HasCleanupHistory = false;
            CleanupHistoryText = "Cleanup history could not be read: " + ex.Message;
            _logger.Error("Could not read cleanup history.", ex);
        }
    }

    private void ExportMarkdown()
    {
        if (_lastReport is null)
        {
            return;
        }

        try
        {
            string path = _exporter.ExportMarkdown(_lastReport);
            StatusMessage = "Markdown report exported: " + path;
            _launcher.OpenFolder(Path.GetDirectoryName(path) ?? _exporter.ReportsFolder);
        }
        catch (Exception ex)
        {
            StatusMessage = "Could not export the Markdown report: " + ex.Message;
            _logger.Error("Markdown report export failed.", ex);
        }
    }

    private void ExportCsv()
    {
        if (_lastReport is null)
        {
            return;
        }

        try
        {
            string path = _exporter.ExportCsv(_lastReport);
            StatusMessage = "CSV report exported: " + path;
            _launcher.OpenFolder(Path.GetDirectoryName(path) ?? _exporter.ReportsFolder);
        }
        catch (Exception ex)
        {
            StatusMessage = "Could not export the CSV report: " + ex.Message;
            _logger.Error("CSV report export failed.", ex);
        }
    }

    private void OpenReportsFolder()
    {
        try
        {
            Directory.CreateDirectory(_exporter.ReportsFolder);
            _launcher.OpenFolder(_exporter.ReportsFolder);
        }
        catch (Exception ex)
        {
            StatusMessage = "Could not open the reports folder: " + ex.Message;
            _logger.Error("Opening the reports folder failed.", ex);
        }
    }

    private string BuildReport(CleanupReport report, string restoreNote)
    {
        var text = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(restoreNote))
        {
            text.AppendLine(restoreNote);
            text.AppendLine();
        }

        text.AppendLine(report.Summary);
        text.AppendLine();

        foreach (CleanupItemResult item in report.Items)
        {
            text.AppendLine($"#{item.Number} {item.Name}: {item.OutcomeText} — {item.Message}");
        }

        if (report.DriveChanges.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("Drive free space (measured before and after):");

            foreach (string change in report.DriveChanges)
            {
                text.AppendLine("  " + change);
            }
        }

        text.AppendLine();
        text.Append($"Duration {report.Duration.TotalSeconds:0.0} s · log: {_cleanupLog.FilePath}");

        return text.ToString();
    }

    private void CancelPlan()
    {
        _pendingPlan = null;
        HasPendingPlan = false;
        ApprovalReviewText = "Plan cancelled. Nothing was deleted.";
        StatusMessage = "Approval cancelled.";
    }

    private void OpenCleanupLogFolder()
    {
        try
        {
            _launcher.OpenFolder(Path.GetDirectoryName(_cleanupLog.FilePath) ?? Environment.CurrentDirectory);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
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
