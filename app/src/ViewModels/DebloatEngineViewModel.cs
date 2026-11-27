using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCManager.Core.Debloat;
using PCManager.Services.Debloat;
using PCManager.Services.Abstractions;

namespace PCManager.ViewModels;

/// <summary>
/// D2's minimal surface: pick one catalog tweak, preview its plan, then read/detect/apply/undo
/// through the journaled engine. D3 replaces this with the full Debloat page.
/// </summary>
public sealed partial class DebloatEngineViewModel : ObservableObject
{
    private static readonly Lazy<DebloatCatalog> Catalog = new(DebloatCatalogLoader.LoadEmbedded);

    private readonly DebloatEngine _engine;

    public DebloatEngineViewModel(DebloatEngine engine)
    {
        _engine = engine;

        foreach (DebloatTweak tweak in Catalog.Value.Tweaks
                     .Where(tweak => tweak.Actions.All(action =>
                         action is not RegistryKeyDeleteAction and not AppxRemoveAction))
                     .OrderBy(tweak => tweak.Category)
                     .ThenBy(tweak => tweak.Title))
        {
            Tweaks.Add(tweak);
        }

        SelectedTweak = Tweaks.FirstOrDefault();

        DetectCommand = new AsyncRelayCommand(DetectAsync, () => !IsBusy && SelectedTweak is not null);
        ApplyCommand = new AsyncRelayCommand(ApplyAsync, () => !IsBusy && SelectedTweak is not null);
        UndoCommand = new AsyncRelayCommand(UndoAsync, () => !IsBusy);
        StatusText = "Choose a tweak and preview it. Apply asks for administrator permission.";
        PreviewPlan();
    }

    public ObservableCollection<DebloatTweak> Tweaks { get; } = new();

    public AsyncRelayCommand DetectCommand { get; }
    public AsyncRelayCommand ApplyCommand { get; }
    public AsyncRelayCommand UndoCommand { get; }

    [ObservableProperty]
    private DebloatTweak? _selectedTweak;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _detectedText = "Not checked";

    [ObservableProperty]
    private string _planText = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    partial void OnSelectedTweakChanged(DebloatTweak? value)
    {
        DetectedText = "Not checked";
        PreviewPlan();
        DetectCommand.NotifyCanExecuteChanged();
        ApplyCommand.NotifyCanExecuteChanged();
    }

    private void PreviewPlan()
    {
        if (SelectedTweak is null)
        {
            PlanText = "No tweaks available.";
            return;
        }

        DebloatPlan plan = _engine.Plan(new[] { SelectedTweak.Id }, OsBuild(), IsWindows11());
        DebloatPlanItem item = plan.Items[0];
        PlanText = item.IsRunnable
            ? $"Plan: ready · risk {SelectedTweak.Risk} · backup + journal + restore point" +
              (SelectedTweak.RequiresReboot ? " · reboot required" : string.Empty)
            : $"Plan: {item.Status} · {item.Reason}";
    }

    private async Task DetectAsync()
    {
        if (SelectedTweak is null)
        {
            return;
        }

        IsBusy = true;

        try
        {
            DebloatHelperOutcome result = await _engine.DetectAsync(SelectedTweak.Id);
            DetectedText = result.Success
                ? result.Detected == true ? "Detected: already applied" : "Detected: not applied"
                : result.Message;
            StatusText = result.Success ? "Detection finished." : result.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ApplyAsync()
    {
        if (SelectedTweak is null)
        {
            return;
        }

        IsBusy = true;

        try
        {
            DebloatRunRecord record = await _engine.ApplyAsync(
                new[] { SelectedTweak.Id }, OsBuild(), IsWindows11());

            StatusText = record.Completed
                ? $"Applied {record.Entries.Count} tweak(s). Restore point: {record.RestorePointStatus}."
                : record.FailureMessage + (record.RolledBack ? " Rolled back." : string.Empty);
            DetectedText = record.Completed ? "Detected: applied" : DetectedText;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task UndoAsync()
    {
        IsBusy = true;

        try
        {
            DebloatHelperOutcome result = await _engine.UndoAsync();
            StatusText = result.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static int OsBuild() => Environment.OSVersion.Version.Build;

    private static bool IsWindows11() => Environment.OSVersion.Version.Build >= 22000;
}
