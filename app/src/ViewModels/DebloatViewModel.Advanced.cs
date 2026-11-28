using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using PCManager.Core.Debloat;
using PCManager.Services.Debloat;

namespace PCManager.ViewModels;

/// <summary>D4 advanced half: typed user target, preset-file import/export and report-only drift.</summary>
public sealed partial class DebloatViewModel
{
    private readonly HashSet<string> _selectedAppIds = new(StringComparer.Ordinal);
    private IDebloatProfileService? _profiles;

    public ObservableCollection<DebloatTargetOption> Targets { get; } = new();

    public RelayCommand RefreshTargetsCommand { get; private set; } = null!;
    public RelayCommand ExportPresetCommand { get; private set; } = null!;
    public RelayCommand ImportPresetCommand { get; private set; } = null!;
    public AsyncRelayCommand DriftCheckCommand { get; private set; } = null!;

    [ObservableProperty]
    private DebloatTargetOption? _selectedTarget;

    [ObservableProperty]
    private string _targetWarning = "Changes apply to your signed-in account.";

    [ObservableProperty]
    private string _driftText = "Run a drift check to re-detect the last journaled run. It never reapplies anything.";

    [ObservableProperty]
    private bool _isDriftRunning;

    [ObservableProperty]
    private string _presetFileText = "Preset files contain tweak and app ids only.";

    public string TargetToken => SelectedTarget?.Token ?? "current";

    private void InitializeAdvanced(IDebloatProfileService? profiles)
    {
        _profiles = profiles;
        RefreshTargetsCommand = new RelayCommand(LoadTargets);
        ExportPresetCommand = new RelayCommand(ExportPreset);
        ImportPresetCommand = new RelayCommand(ImportPreset);
        DriftCheckCommand = new AsyncRelayCommand(CheckDriftAsync, () => !IsBusy && !IsDriftRunning);
    }

    private void LoadTargets()
    {
        Targets.Clear();
        Targets.Add(new DebloatTargetOption(
            "current", "This account", "Changes apply to your signed-in account."));
        Targets.Add(new DebloatTargetOption(
            "default",
            "Default user (sysprep)",
            "SYSPREP TARGET: changes the profile copied to every new account. Use only while preparing an image."));

        if (_profiles is not null)
        {
            foreach (DebloatUserProfile profile in _profiles.GetProfiles().Where(profile => !profile.IsCurrentUser))
            {
                Targets.Add(new DebloatTargetOption(
                    "sid:" + profile.Sid,
                    profile.IsLoaded ? $"{profile.Name} (signed in, live hive)" : profile.Name,
                    profile.IsLoaded
                        ? "This account is signed in; FPM uses its live hive."
                        : $"Offline profile: the helper loads and always unloads {profile.Name}'s NTUSER.DAT."));
            }
        }

        SelectedTarget = Targets[0];
    }

    partial void OnSelectedTargetChanged(DebloatTargetOption? value)
    {
        TargetWarning = value?.Warning ?? "Changes apply to your signed-in account.";
        _appliedIds.Clear();

        foreach (DebloatTweakRow row in _allRows)
        {
            row.IsApplied = false;
        }

        RefreshCommandStates();
        StatusMessage = "Target: " + (value?.DisplayName ?? "This account");
    }

    private void ExportPreset()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export FPM debloat preset",
            FileName = DebloatPresetFile.FileName,
            Filter = "FPM debloat preset (*.json)|*.json|JSON (*.json)|*.json",
            AddExtension = true,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            string json = DebloatPresetFile.Serialize(
                _allRows.Where(row => row.IsSelected && row.IsSelectable).Select(row => row.Tweak.Id),
                _selectedAppIds);

            File.WriteAllText(dialog.FileName, json);
            PresetFileText = $"Exported {_allRows.Count(row => row.IsSelected)} tweak id(s) and " +
                             $"{_selectedAppIds.Count} app id(s) to {dialog.FileName}.";
            StatusMessage = PresetFileText;
        }
        catch (Exception ex)
        {
            PresetFileText = "Export failed: " + ex.Message;
            StatusMessage = PresetFileText;
        }
    }

    private void ImportPreset()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import FPM debloat preset",
            Filter = "FPM debloat preset (*.json)|*.json|JSON (*.json)|*.json",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            DebloatPresetImport import = DebloatPresetFile.Parse(File.ReadAllText(dialog.FileName), _catalog!);
            var ids = import.TweakIds.ToHashSet(StringComparer.Ordinal);

            _applyingPreset = true;
            foreach (DebloatTweakRow row in _allRows)
            {
                row.IsSelected = ids.Contains(row.Tweak.Id) && row.IsSelectable;
            }

            _selectedAppIds.Clear();
            foreach (string appId in import.AppIds)
            {
                _selectedAppIds.Add(appId);
            }

            _applyingPreset = false;
            SelectedPreset = Presets.First(preset => preset.Id == DebloatPresetCatalog.CustomId);
            RebuildApps();
            RecomputePlan();

            PresetFileText = $"Imported {import.TweakIds.Count} tweak id(s) and {import.AppIds.Count} app id(s). " +
                             (import.HasWarnings
                                 ? string.Join(" ", import.Warnings)
                                 : "All ids matched the catalog.");
            StatusMessage = PresetFileText;
        }
        catch (Exception ex)
        {
            PresetFileText = "Import refused: " + ex.Message;
            StatusMessage = PresetFileText;
        }
    }

    private async Task CheckDriftAsync()
    {
        DebloatRunRecord? latest = _engine.LatestRun();

        if (latest is null || latest.Entries.Count == 0)
        {
            DriftText = "No journaled tweak run to check.";
            return;
        }

        IsDriftRunning = true;
        DriftText = $"Checking {latest.Entries.Count} journaled tweak(s) report-only...";
        var items = new List<DebloatDriftItem>();

        try
        {
            foreach (DebloatJournalEntry entry in latest.Entries)
            {
                DebloatHelperOutcome outcome = await _engine.DetectAsync(entry.TweakId, latest.TargetUser);
                items.Add(new DebloatDriftItem(
                    entry.TweakId,
                    entry.Title,
                    outcome.Success && outcome.Detected == true));
            }

            DriftText = DebloatDrift.Summarize(items);
            StatusMessage = DriftText;
        }
        catch (Exception ex)
        {
            DriftText = "Drift check failed: " + ex.Message;
            StatusMessage = DriftText;
        }
        finally
        {
            IsDriftRunning = false;
            DriftCheckCommand.NotifyCanExecuteChanged();
        }
    }

    private static string TargetLabel(string token) => (token ?? string.Empty) switch
    {
        "" or "current" => "this account",
        "default" => "Default user (sysprep)",
        var value when value.StartsWith("sid:", StringComparison.OrdinalIgnoreCase) => value[4..],
        var value => value,
    };
}
