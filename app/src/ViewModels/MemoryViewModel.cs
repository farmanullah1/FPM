using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCManager.Core.Formatting;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.ViewModels;

public sealed partial class SelectableProcessMemoryItem : ObservableObject
{
    public int Id { get; init; }
    public string ProcessName { get; init; } = string.Empty;
    public long WorkingSetBytes { get; init; }
    public long PrivateBytes { get; init; }
    public string FormattedWorkingSet => ByteFormatter.Format(WorkingSetBytes);
    public string FormattedPrivateBytes => ByteFormatter.Format(PrivateBytes);
    public bool CanRelieve { get; init; }
    public bool IsProtected { get; init; }

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>
/// Dedicated Memory Diagnostics & Pressure Management ViewModel (Card M1, §13).
/// Measures real physical memory, commit charge, and performs graceful-first process relief.
/// </summary>
public sealed partial class MemoryViewModel : ViewModelBase
{
    private readonly IMemoryDiagnosticsService _diagnostics;
    private readonly IMemoryReliefService _relief;
    private readonly IUserConfirmationService _confirmation;
    private readonly IAppLogger _logger;

    public MemoryViewModel(
        IMemoryDiagnosticsService diagnostics,
        IMemoryReliefService relief,
        IUserConfirmationService confirmation,
        IAppLogger logger)
    {
        _diagnostics = diagnostics;
        _relief = relief;
        _confirmation = confirmation;
        _logger = logger;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        SelectAllSafeCommand = new RelayCommand(SelectAllSafe);
        DeselectAllCommand = new RelayCommand(DeselectAll);
        RelieveSelectedCommand = new AsyncRelayCommand(RelieveSelectedAsync);
    }

    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand SelectAllSafeCommand { get; }
    public RelayCommand DeselectAllCommand { get; }
    public AsyncRelayCommand RelieveSelectedCommand { get; }

    public ObservableCollection<SelectableProcessMemoryItem> Processes { get; } = new();

    [ObservableProperty]
    private string _totalPhysicalText = "-";

    [ObservableProperty]
    private string _availablePhysicalText = "-";

    [ObservableProperty]
    private uint _loadPercent;

    [ObservableProperty]
    private string _commitChargeText = "-";

    [ObservableProperty]
    private string _pressureText = "Healthy";

    [ObservableProperty]
    private string _pressureDescription = "Memory load is within safe operational limits.";

    [ObservableProperty]
    private string _reliefReportText = string.Empty;

    [ObservableProperty]
    private bool _hasReliefReport;

    public override async Task LoadAsync()
    {
        await RefreshAsync().ConfigureAwait(false);
    }

    public Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            ExtendedMemorySnapshot snapshot = _diagnostics.GetExtendedSnapshot();
            ApplySnapshot(snapshot);

            IReadOnlyList<ProcessMemoryItem> rawProcesses = _relief.GetEligibleProcesses();
            Processes.Clear();

            foreach (ProcessMemoryItem p in rawProcesses.OrderByDescending(x => x.WorkingSetBytes))
            {
                Processes.Add(new SelectableProcessMemoryItem
                {
                    Id = p.Id,
                    ProcessName = p.ProcessName,
                    WorkingSetBytes = p.WorkingSetBytes,
                    PrivateBytes = p.PrivateMemoryBytes,
                    CanRelieve = p.CanRelieve,
                    IsProtected = p.IsProtected,
                    IsSelected = false
                });
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Memory diagnostics refresh failed.", ex);
            StatusMessage = "Failed to refresh memory diagnostics.";
        }
        finally
        {
            IsBusy = false;
        }

        return Task.CompletedTask;
    }

    private void ApplySnapshot(ExtendedMemorySnapshot snapshot)
    {
        TotalPhysicalText = $"{ByteFormatter.Format((long)snapshot.TotalPhysicalBytes)} installed";
        AvailablePhysicalText = $"{ByteFormatter.Format((long)snapshot.AvailablePhysicalBytes)} available ({snapshot.MemoryLoadPercent}% used)";
        LoadPercent = snapshot.MemoryLoadPercent;
        CommitChargeText = $"{ByteFormatter.Format((long)snapshot.CommittedBytes)} / {ByteFormatter.Format((long)snapshot.CommitLimitBytes)} committed";

        switch (snapshot.Pressure)
        {
            case MemoryPressureLevel.High:
                PressureText = "High Pressure";
                PressureDescription = "System memory usage exceeds 85%. Consider closing heavy background applications.";
                break;
            case MemoryPressureLevel.Moderate:
                PressureText = "Moderate Pressure";
                PressureDescription = "Memory load is above 70%. Applications are operating normally.";
                break;
            default:
                PressureText = "Healthy";
                PressureDescription = "Memory load is within optimal limits. No action needed.";
                break;
        }
    }

    private void SelectAllSafe()
    {
        foreach (SelectableProcessMemoryItem item in Processes)
        {
            if (item.CanRelieve)
            {
                item.IsSelected = true;
            }
        }
    }

    private void DeselectAll()
    {
        foreach (SelectableProcessMemoryItem item in Processes)
        {
            item.IsSelected = false;
        }
    }

    private async Task RelieveSelectedAsync()
    {
        var selected = Processes.Where(p => p.IsSelected && p.CanRelieve).ToList();
        if (selected.Count == 0)
        {
            StatusMessage = "No eligible applications selected to close.";
            return;
        }

        string appList = string.Join(", ", selected.Take(5).Select(s => s.ProcessName));
        if (selected.Count > 5)
        {
            appList += $" and {selected.Count - 5} more";
        }

        bool confirmed = _confirmation.Confirm(
            "Close Selected Applications",
            $"Are you sure you want to close {selected.Count} application(s) ({appList})?\n\nPlease save any unsaved work in these applications before proceeding.");

        if (!confirmed)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "Closing selected applications safely...";

        try
        {
            ExtendedMemorySnapshot before = _diagnostics.GetExtendedSnapshot();

            var plan = new MemoryReliefPlan(
                selected.Select(s => s.Id).ToList(),
                GracefulWaitMs: 3000,
                AllowForceKill: false);

            MemoryReliefResult result = await _relief.ExecuteReliefPlanAsync(plan).ConfigureAwait(false);

            ExtendedMemorySnapshot after = _diagnostics.GetExtendedSnapshot();

            long bytesFreed = (long)after.AvailablePhysicalBytes - (long)before.AvailablePhysicalBytes;
            string freedFormatted = bytesFreed > 0 ? ByteFormatter.Format(bytesFreed) : "0 B";

            ReliefReportText = $"Relief complete: Closed {result.SuccessfullyClosed.Count} app(s). " +
                               $"Memory load changed from {before.MemoryLoadPercent}% to {after.MemoryLoadPercent}%. " +
                               $"Observed available memory increase: {freedFormatted}.";
            HasReliefReport = true;

            await RefreshAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error("Memory relief operation failed.", ex);
            StatusMessage = $"Memory relief failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
