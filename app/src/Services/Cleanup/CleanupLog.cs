using System.Text;
using PCManager.Core;
using PCManager.Core.Formatting;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Cleanup;

/// <summary>
/// Append-only audit trail at %LOCALAPPDATA%\FarmanullahPCManager\logs\cleanup_log.txt.
/// One line per item. Rotated at 5 MB so a chatty clean cannot fill the disk.
/// </summary>
public sealed class CleanupLog : ICleanupLog
{
    private const long MaxBytes = 5 * 1024 * 1024;

    private readonly object _gate = new();
    private readonly IAppLogger _logger;

    public CleanupLog(IAppLogger logger)
    {
        _logger = logger;
        FilePath = Path.Combine(AppInfo.LogFolder, "cleanup_log.txt");
    }

    public string FilePath { get; }

    public void Write(CleanupPlanItem item, CleanupItemOutcome outcome, long bytes, string detail)
    {
        string paths = item.Roots.Count == 0 ? "Recycle Bin" : string.Join(" | ", item.Roots);

        WriteLine(
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\t#{item.Number}\t{item.RuleId}\t{paths}\t" +
            $"{ByteFormatter.Format(bytes)}\t{OutcomeText(outcome)}\t{detail}");
    }

    public void WriteLine(string message)
    {
        try
        {
            lock (_gate)
            {
                Directory.CreateDirectory(AppInfo.LogFolder);
                RotateIfNeeded();

                File.AppendAllText(FilePath, message + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch (Exception ex)
        {
            // Logging must never abort a cleanup; record the failure in the app log.
            _logger.Error("Could not append to the cleanup log.", ex);
        }
    }

    private static string OutcomeText(CleanupItemOutcome outcome) => outcome switch
    {
        CleanupItemOutcome.Deleted => "DELETED",
        CleanupItemOutcome.PartiallyDeleted => "PARTIAL",
        CleanupItemOutcome.Quarantined => "QUARANTINED",
        CleanupItemOutcome.PartiallyQuarantined => "PARTIAL_QUARANTINE",
        CleanupItemOutcome.DryRun => "DRYRUN",
        CleanupItemOutcome.SkippedProtected => "SKIPPED_PROTECTED",
        CleanupItemOutcome.SkippedLocked => "SKIPPED_LOCKED",
        CleanupItemOutcome.SkippedElevation => "SKIPPED_ELEVATION",
        CleanupItemOutcome.SkippedRunning => "SKIPPED_RUNNING",
        CleanupItemOutcome.ChangedSinceScan => "CHANGED_SINCE_SCAN",
        CleanupItemOutcome.Failed => "FAILED",
        _ => "UNKNOWN"
    };

    private void RotateIfNeeded()
    {
        var file = new FileInfo(FilePath);

        if (!file.Exists || file.Length <= MaxBytes)
        {
            return;
        }

        string previous = Path.Combine(AppInfo.LogFolder, "cleanup_log.1.txt");
        File.Move(FilePath, previous, overwrite: true);
    }
}
