using System.Diagnostics;
using PCManager.Core.Formatting;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;
using PCManager.Services.Infrastructure;

namespace PCManager.Services.Cleanup;

/// <summary>
/// Phase 2. Deletes only what an approved <see cref="CleanupPlan"/> names, re-validates every
/// file through <see cref="ISafetyGuard"/>, skips locked or protected files without aborting,
/// writes each outcome to cleanup_log.txt and measures the folders again afterwards.
///
/// The plan expires after 15 minutes so a stale approval cannot be replayed.
/// </summary>
public sealed class CleanupExecutor : ICleanupExecutor
{
    private readonly ISafetyGuard _guard;
    private readonly ICleanupLog _log;
    private readonly IAppLogger _logger;
    private readonly IRecycleBinService _recycleBin;

    public CleanupExecutor(
        ISafetyGuard guard,
        ICleanupLog log,
        IAppLogger logger,
        IRecycleBinService recycleBin)
    {
        _guard = guard;
        _log = log;
        _logger = logger;
        _recycleBin = recycleBin;
    }

    public Task<CleanupReport> ExecuteAsync(CleanupPlan plan, bool dryRun, CancellationToken cancellationToken = default)
        => Task.Run(() => Execute(plan, dryRun, cancellationToken), cancellationToken);

    private CleanupReport Execute(CleanupPlan plan, bool dryRun, CancellationToken cancellationToken)
    {
        if (DateTime.UtcNow > plan.ExpiresUtc)
        {
            _log.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\tPLAN_EXPIRED\t{plan.PlanId}");
            return new CleanupReport
            {
                EstimatedBytes = plan.TotalBytes,
                DryRun = dryRun,
                Expired = true,
                Summary = "The approval expired after 15 minutes. Nothing was deleted. Scan again and approve a fresh plan."
            };
        }

        var stopwatch = Stopwatch.StartNew();
        Dictionary<string, long> freeBefore = CaptureFreeSpace(plan.Items);
        string mode = dryRun ? "DRYRUN" : "EXECUTE";

        _log.WriteLine(
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\tPLAN_START\t{plan.PlanId}\t{mode}\t" +
            $"{plan.Items.Count} item(s)\t{ByteFormatter.Format(plan.TotalBytes)}");

        var results = new List<CleanupItemResult>();

        foreach (CleanupPlanItem item in plan.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(ExecuteItem(item, dryRun, cancellationToken));
        }

        stopwatch.Stop();

        if (dryRun)
        {
            _log.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\tPLAN_END\t{plan.PlanId}\tDRYRUN\t0 B deleted");
        }
        else
        {
            long released = results.Sum(result => result.BytesDeleted);
            _log.WriteLine(
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\tPLAN_END\t{plan.PlanId}\t" +
                $"{ByteFormatter.Format(released)} deleted\t{results.Sum(r => r.FilesDeleted)} file(s)");
        }

        Dictionary<string, long> freeAfter = CaptureFreeSpace(plan.Items);
        var driveChanges = new List<string>();

        foreach ((string drive, long before) in freeBefore)
        {
            if (!freeAfter.TryGetValue(drive, out long after))
            {
                continue;
            }

            long delta = after - before;
            driveChanges.Add($"{drive} {ByteFormatter.Format(before)} → {ByteFormatter.Format(after)} free " +
                             $"({(delta >= 0 ? "+" : "-")}{ByteFormatter.Format(Math.Abs(delta))})");
        }

        long bytesDeleted = results.Sum(result => result.BytesDeleted);
        int filesDeleted = results.Sum(result => result.FilesDeleted);
        int filesSkipped = results.Sum(result => result.FilesSkipped);
        int itemCount = results.Count;

        string summary;

        if (dryRun)
        {
            summary = $"Dry run: {itemCount} item(s), {ByteFormatter.Format(plan.TotalBytes)} would be removed. " +
                      "Nothing was deleted.";
        }
        else
        {
            var skipped = results.Count(result => result.Outcome is not (CleanupItemOutcome.Deleted or CleanupItemOutcome.PartiallyDeleted));

            summary = $"Deleted {results.Count(r => r.Outcome is CleanupItemOutcome.Deleted or CleanupItemOutcome.PartiallyDeleted)} " +
                      $"of {itemCount} item(s). Space released: {ByteFormatter.Format(bytesDeleted)} " +
                      $"(estimate was {ByteFormatter.Format(plan.TotalBytes)}); {filesDeleted:N0} file(s) removed";

            if (filesSkipped > 0)
            {
                summary += $", {filesSkipped:N0} skipped";
            }

            if (skipped > 0)
            {
                summary += $"; {skipped} item(s) skipped";
            }

            summary += ".";

            if (driveChanges.Count > 0)
            {
                summary += " " + string.Join("; ", driveChanges) + ".";
            }

            summary += " File totals and drive free space can differ because of compression, open handles and other disk activity.";
        }

        return new CleanupReport
        {
            Items = results,
            EstimatedBytes = plan.TotalBytes,
            BytesDeleted = bytesDeleted,
            FilesDeleted = filesDeleted,
            FilesSkipped = filesSkipped,
            Duration = stopwatch.Elapsed,
            DryRun = dryRun,
            DriveChanges = driveChanges,
            Summary = summary
        };
    }

    private CleanupItemResult ExecuteItem(
        CleanupPlanItem item,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        if (item.RequiresAppClosed && IsRunning(item.ProcessName))
        {
            _log.Write(item, CleanupItemOutcome.SkippedRunning, 0, "application is running");
            return Result(item, CleanupItemOutcome.SkippedRunning, 0, 0, 0, 0,
                "Skipped: the application is running. Close it and clean again.");
        }

        if (item.Action == CleanupActionKind.EmptyRecycleBin)
        {
            long size = TryRecycleSize();

            if (dryRun)
            {
                _log.Write(item, CleanupItemOutcome.DryRun, 0, $"{ByteFormatter.Format(size)} would be emptied");
                return Result(item, CleanupItemOutcome.DryRun, 0, 0, 0, size,
                    $"Dry run: {ByteFormatter.Format(size)} would be emptied. Nothing was removed.");
            }

            try
            {
                long freed = _recycleBin.Empty();
                long remaining = TryRecycleSize();
                var outcome = freed > 0 ? CleanupItemOutcome.Deleted : CleanupItemOutcome.Failed;

                _log.Write(item, outcome, freed, "Recycle Bin emptied through the shell API");

                return Result(item, outcome, freed, 0, 0, remaining,
                    freed > 0
                        ? $"{ByteFormatter.Format(freed)} released by emptying the Recycle Bin."
                        : "The Recycle Bin was already empty or could not be emptied.");
            }
            catch (Exception ex)
            {
                _logger.Error("Emptying the Recycle Bin failed.", ex);
                _log.Write(item, CleanupItemOutcome.Failed, 0, ex.Message);
                return Result(item, CleanupItemOutcome.Failed, 0, 0, 0, size,
                    $"Failed: {ex.Message}");
            }
        }

        long deletedBytes = 0;
        int deletedFiles = 0;
        int skippedLocked = 0;
        int skippedProtected = 0;
        int skippedElevation = 0;
        int failed = 0;
        int eligible = 0;

        DateTime cutoff = DateTime.UtcNow - item.MinimumAge;

        foreach (string root in item.Roots)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!Directory.Exists(root))
            {
                continue;
            }

            SafetyVerdict rootVerdict = _guard.EvaluateRoot(root, item.Roots);

            if (!rootVerdict.Allowed)
            {
                skippedProtected++;
                _log.Write(item, CleanupItemOutcome.SkippedProtected, 0, $"root refused: {rootVerdict.Reason}");
                continue;
            }

            foreach (FileInfo file in SafeFileSystem.EnumerateFiles(root, recurse: true))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    if (file.LastWriteTimeUtc > cutoff)
                    {
                        continue;
                    }

                    eligible++;

                    SafetyVerdict verdict = _guard.Evaluate(file.FullName, item.Roots);

                    if (!verdict.Allowed)
                    {
                        skippedProtected++;
                        continue;
                    }

                    if (dryRun)
                    {
                        deletedBytes += file.Length;
                        deletedFiles++;
                        continue;
                    }

                    long size = file.Length;

                    if ((file.Attributes & FileAttributes.ReadOnly) != 0)
                    {
                        file.Attributes = FileAttributes.Normal;
                    }

                    file.Delete();
                    deletedBytes += size;
                    deletedFiles++;
                }
                catch (UnauthorizedAccessException)
                {
                    skippedElevation++;
                }
                catch (IOException)
                {
                    skippedLocked++;
                }
                catch (Exception ex)
                {
                    failed++;
                    _logger.Error($"Cleanup failed for {file.FullName}.", ex);
                }
            }

            if (!dryRun)
            {
                DeleteEmptySubfolders(root, item, cancellationToken);
            }
        }

        long remainingBytes = dryRun
            ? 0
            : CleanupScanner.Measure(item.Roots, TimeSpan.Zero, cancellationToken).Bytes;

        CleanupItemOutcome itemOutcome;
        string message;

        if (dryRun)
        {
            itemOutcome = CleanupItemOutcome.DryRun;
            message = $"Dry run: {ByteFormatter.Format(deletedBytes)} across {deletedFiles:N0} file(s) would be removed. " +
                      "Nothing was removed.";
        }
        else if (eligible == 0)
        {
            itemOutcome = CleanupItemOutcome.Deleted;
            message = "Nothing matched the rule at execution time; the folder was already clean or changed since the scan.";
        }
        else if (deletedFiles > 0 && skippedLocked + skippedProtected + skippedElevation + failed > 0)
        {
            itemOutcome = CleanupItemOutcome.PartiallyDeleted;
            message = $"{ByteFormatter.Format(deletedBytes)} removed from {deletedFiles:N0} file(s); " +
                      $"{skippedLocked + skippedProtected + skippedElevation + failed:N0} skipped.";
        }
        else if (deletedFiles > 0)
        {
            itemOutcome = CleanupItemOutcome.Deleted;
            message = $"{ByteFormatter.Format(deletedBytes)} removed from {deletedFiles:N0} file(s).";
        }
        else if (skippedProtected > 0)
        {
            itemOutcome = CleanupItemOutcome.SkippedProtected;
            message = "Skipped: the safety guard refused the files in this folder.";
        }
        else if (skippedElevation > 0)
        {
            itemOutcome = CleanupItemOutcome.SkippedElevation;
            message = "Skipped: administrator rights are required for these files.";
        }
        else if (skippedLocked > 0)
        {
            itemOutcome = CleanupItemOutcome.SkippedLocked;
            message = "Skipped: the files are in use by another process.";
        }
        else
        {
            itemOutcome = CleanupItemOutcome.Failed;
            message = "No file could be removed; see the cleanup log for details.";
        }

        _log.Write(item, itemOutcome, dryRun ? 0 : deletedBytes, message);

        return Result(
            item,
            itemOutcome,
            dryRun ? 0 : deletedBytes,
            dryRun ? 0 : deletedFiles,
            skippedLocked + skippedProtected + skippedElevation + failed,
            remainingBytes,
            message);
    }

    private void DeleteEmptySubfolders(string root, CleanupPlanItem item, CancellationToken cancellationToken)
    {
        try
        {
            var directories = new DirectoryInfo(root)
                .EnumerateDirectories("*", new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System
                })
                .OrderByDescending(directory => directory.FullName.Length)
                .ToList();

            foreach (DirectoryInfo directory in directories)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!_guard.Evaluate(directory.FullName, item.Roots).Allowed)
                {
                    continue;
                }

                try
                {
                    if (!directory.EnumerateFileSystemInfos().Any())
                    {
                        directory.Delete();
                    }
                }
                catch
                {
                    // Kept because something still uses it; the next run can try again.
                }
            }
        }
        catch
        {
            // Enumeration is best-effort; the root itself is never removed.
        }
    }

    private static CleanupItemResult Result(
        CleanupPlanItem item,
        CleanupItemOutcome outcome,
        long bytesDeleted,
        int deletedFiles,
        int skippedFiles,
        long bytesRemaining,
        string message)
        => new()
        {
            Number = item.Number,
            Name = item.Name,
            PathText = item.Roots.Count == 0 ? "Recycle Bin" : string.Join(" | ", item.Roots),
            BytesDeleted = bytesDeleted,
            BytesRemaining = bytesRemaining,
            FilesDeleted = deletedFiles,
            FilesSkipped = skippedFiles,
            Outcome = outcome,
            Message = message
        };

    private long TryRecycleSize()
    {
        try
        {
            return _recycleBin.GetSize();
        }
        catch
        {
            return 0;
        }
    }

    private static Dictionary<string, long> CaptureFreeSpace(IEnumerable<CleanupPlanItem> items)
    {
        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        foreach (string root in items.SelectMany(item => item.Roots))
        {
            string? drive = Path.GetPathRoot(root);

            if (string.IsNullOrWhiteSpace(drive) || result.ContainsKey(drive))
            {
                continue;
            }

            try
            {
                var info = new DriveInfo(drive);

                if (info.IsReady)
                {
                    result[drive] = info.AvailableFreeSpace;
                }
            }
            catch
            {
                // A disconnected drive is simply not reported.
            }
        }

        return result;
    }

    private static bool IsRunning(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        try
        {
            return Process.GetProcessesByName(processName).Length > 0;
        }
        catch
        {
            return false;
        }
    }
}
