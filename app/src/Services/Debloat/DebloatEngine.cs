using System.IO;
using PCManager.Core;
using PCManager.Core.Debloat;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Debloat;

/// <summary>
/// Plans, backs up, applies through the helper, journals the exact pre-state and undoes in reverse.
/// A failed run rolls back everything it already applied and records that truthfully.
/// </summary>
public sealed class DebloatEngine
{
    private static readonly Lazy<DebloatCatalog> Catalog = new(DebloatCatalogLoader.LoadEmbedded);

    private readonly IDebloatExecutor _executor;
    private readonly IRestorePointService _restorePoint;
    private readonly IAppLogger _logger;
    private readonly DebloatJournalStore _journal;
    private readonly string _backupDirectory;

    public DebloatEngine(
        IDebloatExecutor executor,
        IRestorePointService restorePoint,
        IAppLogger logger,
        DebloatJournalStore? journal = null,
        string? backupDirectory = null)
    {
        _executor = executor;
        _restorePoint = restorePoint;
        _logger = logger;
        _journal = journal ?? new DebloatJournalStore();
        _backupDirectory = backupDirectory ?? Path.Combine(AppInfo.DataFolder, "debloat", "backups");
    }

    public DebloatPlan Plan(IEnumerable<string> tweakIds, int osBuild, bool isWindows11) =>
        DebloatPlanner.Plan(Catalog.Value, tweakIds, osBuild, isWindows11);

    public DebloatRunRecord? LatestRun() => _journal.LoadLatest();

    public IReadOnlyList<DebloatRunRecord> Runs(int limit = 20) => _journal.LoadAll(limit);

    public async Task<DebloatHelperOutcome> DetectAsync(
        string tweakId,
        string targetUser = "",
        CancellationToken cancellationToken = default)
    {
        DebloatTweak? tweak = Catalog.Value.Tweaks.FirstOrDefault(candidate => candidate.Id == tweakId);
        return tweak is null
            ? DebloatHelperOutcome.Failed($"Unknown tweak '{tweakId}'.")
            : await _executor.DetectAsync(tweak, targetUser, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DebloatRunRecord> ApplyAsync(
        IReadOnlyCollection<string> tweakIds,
        int osBuild,
        bool isWindows11,
        bool createRestorePoint = true,
        string targetUser = "",
        CancellationToken cancellationToken = default)
    {
        DebloatPlan plan = Plan(tweakIds, osBuild, isWindows11);
        var record = new DebloatRunRecord
        {
            RunId = $"{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..6]}",
            StartedAtUtc = DateTime.UtcNow,
            RequiresReboot = plan.RequiresReboot,
            RequiresExplorerRestart = plan.RequiresExplorerRestart,
            TargetUser = targetUser,
        };

        List<DebloatTweak> runnable = plan.Runnable.ToList();
        if (runnable.Count == 0)
        {
            record.Completed = true;
            record.FinishedAtUtc = DateTime.UtcNow;
            record.FailureMessage = "Nothing to apply: every selected tweak was skipped.";
            return record;
        }

        record.RestorePointStatus = await TryRestorePointAsync(plan, createRestorePoint, cancellationToken)
            .ConfigureAwait(false);

        // Detect first: a tweak already applied is skipped, and the values captured here are the
        // exact pre-state that both the .reg backup and the journal will hold.
        var preStates = new Dictionary<string, List<RegistryValueState>>(StringComparer.Ordinal);
        var toApply = new List<DebloatTweak>();

        foreach (DebloatTweak tweak in runnable)
        {
            DebloatHelperOutcome detected = await _executor
                .DetectAsync(tweak, targetUser, cancellationToken)
                .ConfigureAwait(false);

            if (!detected.Success)
            {
                record.FailureMessage = detected.Cancelled
                    ? "Administrator permission was declined before anything changed."
                    : $"Detection failed for '{tweak.Title}': {detected.Message}";
                record.FinishedAtUtc = DateTime.UtcNow;
                _journal.Save(record);
                return record;
            }

            if (detected.Detected == true)
            {
                continue;
            }

            preStates[tweak.Id] = detected.Values.ToList();
            toApply.Add(tweak);
        }

        if (toApply.Count == 0)
        {
            record.Completed = true;
            record.FinishedAtUtc = DateTime.UtcNow;
            record.FailureMessage = "Every selected tweak is already applied.";
            _journal.Save(record);
            return record;
        }

        record.BackupPath = WriteBackup(record.RunId, preStates.Values.SelectMany(states => states));
        _journal.Save(record);

        foreach (DebloatTweak tweak in toApply)
        {
            DebloatHelperOutcome applied = await _executor
                .ApplyAsync(tweak, targetUser, cancellationToken)
                .ConfigureAwait(false);

            if (!applied.Success)
            {
                record.FailureMessage =
                    $"'{tweak.Title}' failed: {applied.Message}" +
                    (applied.RolledBack ? " (the tweak's own partial changes were restored)" : string.Empty);
                record.RolledBack = await RollbackAsync(record, cancellationToken).ConfigureAwait(false);
                record.FinishedAtUtc = DateTime.UtcNow;
                _journal.Save(record);
                _logger.Error($"Debloat run {record.RunId} failed: {record.FailureMessage}");
                return record;
            }

            record.Entries.Add(new DebloatJournalEntry
            {
                TweakId = tweak.Id,
                Title = tweak.Title,
                AppliedAtUtc = DateTime.UtcNow,
                Before = preStates[tweak.Id],
            });
            _journal.Save(record);
        }

        record.Completed = true;
        record.FinishedAtUtc = DateTime.UtcNow;
        _journal.Save(record);
        _logger.Info(
            $"Debloat run {record.RunId}: {record.Entries.Count} tweak(s), restore point " +
            $"{record.RestorePointStatus}, backup {record.BackupPath}.");
        return record;
    }

    public async Task<DebloatHelperOutcome> UndoAsync(CancellationToken cancellationToken = default)
    {
        DebloatRunRecord? record = _journal.LoadLatest();

        if (record is null)
        {
            return DebloatHelperOutcome.Failed("There is no debloat run to undo.");
        }

        if (record.RolledBack)
        {
            return DebloatHelperOutcome.Failed("That run was already rolled back.");
        }

        if (record.Undone)
        {
            return DebloatHelperOutcome.Failed("That run has already been undone.");
        }

        for (int index = record.Entries.Count - 1; index >= 0; index--)
        {
            DebloatJournalEntry entry = record.Entries[index];
            DebloatTweak? tweak = Catalog.Value.Tweaks.FirstOrDefault(candidate => candidate.Id == entry.TweakId);

            if (tweak is null)
            {
                return DebloatHelperOutcome.Failed($"Journal references unknown tweak '{entry.TweakId}'.");
            }

            DebloatHelperOutcome reverted = await _executor
                .RevertAsync(tweak, entry.Before, record.TargetUser, cancellationToken)
                .ConfigureAwait(false);

            if (!reverted.Success)
            {
                _journal.Save(record);
                return DebloatHelperOutcome.Failed(
                    $"Undo stopped at '{entry.Title}': {reverted.Message}");
            }
        }

        record.Undone = true;
        _journal.Save(record);
        return new DebloatHelperOutcome
        {
            Success = true,
            Message = $"Restored {record.Entries.Count} tweak(s) from run {record.RunId}.",
        };
    }

    private async Task<bool> RollbackAsync(DebloatRunRecord record, CancellationToken cancellationToken)
    {
        bool allRestored = true;

        for (int index = record.Entries.Count - 1; index >= 0; index--)
        {
            DebloatJournalEntry entry = record.Entries[index];
            DebloatTweak? tweak = Catalog.Value.Tweaks.FirstOrDefault(candidate => candidate.Id == entry.TweakId);

            if (tweak is null)
            {
                allRestored = false;
                continue;
            }

            DebloatHelperOutcome reverted = await _executor
                .RevertAsync(tweak, entry.Before, record.TargetUser, cancellationToken)
                .ConfigureAwait(false);

            allRestored &= reverted.Success;
        }

        return allRestored;
    }

    private async Task<string> TryRestorePointAsync(
        DebloatPlan plan,
        bool createRestorePoint,
        CancellationToken cancellationToken)
    {
        if (!createRestorePoint)
        {
            return "Skipped: turned off for this run";
        }

        if (!plan.HasHighRisk && !plan.Items.Any(item =>
                item.IsRunnable && !string.Equals(item.Tweak.Risk, "Low", StringComparison.Ordinal)))
        {
            return "Skipped: low-risk plan";
        }

        try
        {
            var outcome = await _restorePoint.CreateAsync(cancellationToken).ConfigureAwait(false);
            return outcome.Success ? "Created" : $"Skipped: {outcome.Message}";
        }
        catch (Exception ex)
        {
            _logger.Error("Restore point creation failed.", ex);
            return $"Skipped: {ex.Message}";
        }
    }

    private string WriteBackup(string runId, IEnumerable<RegistryValueState> states)
    {
        Directory.CreateDirectory(_backupDirectory);
        string path = Path.Combine(_backupDirectory, $"{runId}.reg");
        File.WriteAllText(path, DebloatRegBackup.Build(states, DateTime.UtcNow));
        return path;
    }
}
