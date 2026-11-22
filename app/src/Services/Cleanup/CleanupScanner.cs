using System.Diagnostics;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;
using PCManager.Services.Infrastructure;

namespace PCManager.Services.Cleanup;

/// <summary>
/// Phase 1. Read-only by construction: this class enumerates folders, reads lengths and
/// timestamps, and never references a delete, move or shell API.
///
/// Only rule roots that exist and are not whitelisted are measured. Files younger than a
/// rule's minimum age are excluded so the displayed size matches what the executor may
/// actually remove.
/// </summary>
public sealed class CleanupScanner : ICleanupScanner
{
    private const int MaxDirectoriesPerRoot = 20_000;

    private readonly ICleanupRuleCatalog _catalog;
    private readonly IWhitelistService _whitelist;
    private readonly IRecycleBinService _recycleBin;

    public CleanupScanner(
        ICleanupRuleCatalog catalog,
        IWhitelistService whitelist,
        IRecycleBinService recycleBin)
    {
        _catalog = catalog;
        _whitelist = whitelist;
        _recycleBin = recycleBin;
    }

    public Task<CleanupScanResult> ScanAsync(CancellationToken cancellationToken = default)
        => Task.Run(() => Scan(cancellationToken), cancellationToken);

    private CleanupScanResult Scan(CancellationToken cancellationToken)
    {
        var items = new List<CleanupCandidate>();
        int number = 0;

        foreach (CleanupRule rule in _catalog.GetRules())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (rule.Action == CleanupActionKind.EmptyRecycleBin)
            {
                long recycleBytes = TryMeasureRecycleBin();

                if (recycleBytes <= 0)
                {
                    continue;
                }

                items.Add(new CleanupCandidate
                {
                    Number = ++number,
                    Rule = rule,
                    Bytes = recycleBytes,
                    FileCount = 0,
                    IsSelected = rule.Tier == CleanupTier.Safe,
                    OldestWriteUtc = null,
                    NewestWriteUtc = null
                });

                continue;
            }

            string[] roots = rule.Roots
                .Where(Directory.Exists)
                .Where(root => !_whitelist.IsWhitelisted(root))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (roots.Length == 0)
            {
                continue;
            }

            Measurement measurement = Measure(roots, rule.MinimumAge, cancellationToken);

            if (measurement.Bytes == 0 && measurement.FileCount == 0)
            {
                continue;
            }

            bool running = rule.RequiresAppClosed && IsRunning(rule.ProcessName);

            items.Add(new CleanupCandidate
            {
                Number = ++number,
                Rule = rule,
                Roots = roots,
                Bytes = measurement.Bytes,
                FileCount = measurement.FileCount,
                FolderCount = measurement.FolderCount,
                OldestWriteUtc = measurement.Oldest,
                NewestWriteUtc = measurement.Newest,
                IsSelected = rule.Tier == CleanupTier.Safe && !running,
                IsRunning = running
            });
        }

        return new CleanupScanResult
        {
            Items = items,
            Notes = _catalog.GetDiscoveryNotes(),
            ScannedUtc = DateTime.UtcNow
        };
    }

    private long TryMeasureRecycleBin()
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

    internal static Measurement Measure(
        IReadOnlyList<string> roots,
        TimeSpan minimumAge,
        CancellationToken cancellationToken)
    {
        long bytes = 0;
        int files = 0;
        int folders = 0;
        DateTime? oldest = null;
        DateTime? newest = null;
        DateTime cutoff = DateTime.UtcNow - minimumAge;

        foreach (string root in roots)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (FileInfo file in SafeFileSystem.EnumerateFiles(root, recurse: true))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    DateTime written = file.LastWriteTimeUtc;

                    if (written > cutoff)
                    {
                        continue;
                    }

                    bytes += file.Length;
                    files++;

                    if (oldest is null || written < oldest)
                    {
                        oldest = written;
                    }

                    if (newest is null || written > newest)
                    {
                        newest = written;
                    }
                }
                catch
                {
                    // File vanished mid-scan; it contributes nothing.
                }
            }

            folders += CountDirectories(root);
        }

        return new Measurement(bytes, files, folders, oldest, newest);
    }

    private static int CountDirectories(string root)
    {
        try
        {
            return new DirectoryInfo(root)
                .EnumerateDirectories("*", new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System
                })
                .Take(MaxDirectoriesPerRoot)
                .Count();
        }
        catch
        {
            return 0;
        }
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

    internal readonly record struct Measurement(
        long Bytes,
        int FileCount,
        int FolderCount,
        DateTime? Oldest,
        DateTime? Newest);
}
