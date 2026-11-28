using PCManager.Core.Debloat;

namespace PCManager.ViewModels;

/// <summary>D3 apply/detect/undo, report and history half of the Debloat page view model.</summary>
public sealed partial class DebloatViewModel
{
    private async Task ApplyAsync()
    {
        if (_catalog is null)
        {
            return;
        }

        List<string> ids = _allRows
            .Where(row => row.IsSelected && row.IsSelectable)
            .Select(row => row.Tweak.Id)
            .ToList();

        if (ids.Count == 0)
        {
            return;
        }

        IsBusy = true;
        IsRunning = true;
        IsReviewOpen = false;
        IsReportVisible = false;
        HasError = false;
        RunningText = $"Applying {ids.Count} tweak(s). Approve the Windows administrator prompt(s); the helper journals each change.";

        try
        {
            DebloatRunRecord record = await _engine.ApplyAsync(
                ids, _osBuild, _isWindows11, CreateRestorePoint);

            foreach (DebloatJournalEntry entry in record.Entries)
            {
                _appliedIds.Add(entry.TweakId);
            }

            foreach (DebloatTweakRow row in _allRows)
            {
                row.IsApplied = _appliedIds.Contains(row.Tweak.Id);
            }

            DebloatRunReport report = DebloatRunReport.From(record, ids.Count);
            ShowReport(report, record.FailureMessage);
            RefreshHistory();
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorText = $"The debloat run failed before reporting: {ex.Message}";
            ShowReport(new DebloatRunReport
            {
                IsFailure = true,
                Headline = "The run failed",
                Detail = ErrorText,
            }, ex.Message);
        }
        finally
        {
            IsBusy = false;
            IsRunning = false;
            RefreshCommandStates();
        }
    }

    private async Task DetectAsync()
    {
        List<DebloatTweakRow> rows = _allRows
            .Where(row => row.IsSelected && row.IsSelectable)
            .ToList();

        if (rows.Count == 0)
        {
            return;
        }

        IsBusy = true;
        IsRunning = true;
        IsReportVisible = false;
        RunningText = $"Detecting {rows.Count} tweak(s). Windows may ask for administrator permission for each protected read.";
        int applied = 0;
        string? failure = null;

        try
        {
            foreach (DebloatTweakRow row in rows)
            {
                DebloatHelperOutcome outcome = await _engine.DetectAsync(row.Tweak.Id);

                if (!outcome.Success)
                {
                    failure = outcome.Message;
                    break;
                }

                row.IsApplied = outcome.Detected == true;
                if (outcome.Detected == true)
                {
                    applied++;
                }
            }

            if (failure is not null)
            {
                ShowReport(new DebloatRunReport
                {
                    IsFailure = true,
                    Headline = "Detection stopped",
                    Detail = failure,
                }, failure);
            }
            else
            {
                ShowReport(new DebloatRunReport
                {
                    Headline = "Detection finished",
                    Detail = $"{applied} of {rows.Count} selected tweak(s) are already applied. " +
                             "Apply leaves those untouched.",
                }, string.Empty);
            }
        }
        finally
        {
            IsBusy = false;
            IsRunning = false;
            RefreshCommandStates();
        }
    }

    private async Task UndoAsync()
    {
        IsBusy = true;
        IsRunning = true;
        IsReportVisible = false;
        RunningText = "Restoring the last run from its journal, in reverse order.";

        try
        {
            DebloatHelperOutcome outcome = await _engine.UndoAsync();

            if (outcome.Success)
            {
                _appliedIds.Clear();
                foreach (DebloatTweakRow row in _allRows)
                {
                    row.IsApplied = false;
                }
            }

            ShowReport(new DebloatRunReport
            {
                IsFailure = !outcome.Success,
                Headline = outcome.Success ? "Last run undone" : "Undo failed",
                Detail = outcome.Message,
            }, outcome.Message);
            RefreshHistory();
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorText = ex.Message;
            ShowReport(new DebloatRunReport
            {
                IsFailure = true,
                Headline = "Undo failed",
                Detail = ex.Message,
            }, ex.Message);
        }
        finally
        {
            IsBusy = false;
            IsRunning = false;
            RefreshCommandStates();
        }
    }

    private void ShowReport(DebloatRunReport report, string status)
    {
        ReportHeadline = report.Headline;
        ReportDetail = report.Detail;
        ReportIsFailure = report.IsFailure;
        ReportRolledBack = report.RolledBack;
        CanUndo = report.CanUndo;
        IsReportVisible = true;
        StatusMessage = status.Length > 0 ? status : report.Headline;
    }

    private void RefreshHistory()
    {
        IReadOnlyList<DebloatRunRecord> records = _engine.Runs(limit: 10);
        DebloatRunRecord? latest = records.FirstOrDefault();

        HistoryRows.Clear();
        foreach (DebloatRunRecord record in records)
        {
            HistoryRows.Add($"{record.StartedAtUtc.ToLocalTime():g} · {record.Entries.Count} applied · " +
                            (record.RolledBack ? "partially rolled back" : record.Undone ? "undone" : "journaled") +
                            $" · {record.RunId}");
        }

        HistoryCanUndo = latest is not null && latest.Completed && !latest.Undone &&
                          !latest.RolledBack && latest.Entries.Count > 0;
        HistoryText = latest is null
            ? "No debloat runs recorded yet."
            : $"Latest run {latest.RunId} · restore point {latest.RestorePointStatus}";
    }

}
