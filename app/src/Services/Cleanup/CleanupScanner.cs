using System.Collections.Concurrent;
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
/// actually remove. Independent rules are measured with bounded parallelism; four workers
/// keep a large npm cache from serialising the whole scan.
/// </summary>
public sealed class CleanupScanner : ICleanupScanner
{
    private const int MaxFilesPerRoot = 250_000;
    private const int MaxTopLevelSubfolders = 5_000;

    private readonly ICleanupRuleCatalog _catalog;
    private readonly IWhitelistService _whitelist;
    private readonly IRecycleBinService _recycleBin;
    private readonly IAppLogger _logger;

    public CleanupScanner(
        ICleanupRuleCatalog catalog,
        IWhitelistService whitelist,
        IRecycleBinService recycleBin,
        IAppLogger logger)
    {
        _catalog = catalog;
        _whitelist = whitelist;
        _recycleBin = recycleBin;
        _logger = logger;
    }

    public Task<CleanupScanResult> ScanAsync(CancellationToken cancellationToken = default)
        => Task.Run(() => Scan(cancellationToken), cancellationToken);

    private CleanupScanResult Scan(CancellationToken cancellationToken)
    {
        var total = Stopwatch.StartNew();
        var items = new ConcurrentBag<CleanupCandidate>();
        IReadOnlyList<CleanupRule> rules = _catalog.GetRules();

        long recycleBytes = TryMeasureRecycleBin();

        if (recycleBytes > 0)
        {
            items.Add(new CleanupCandidate
            {
                Rule = rules.First(rule => rule.Action == CleanupActionKind.EmptyRecycleBin),
                Bytes = recycleBytes,
                IsSelected = true
            });
        }

        var options = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Math.Max(2, Math.Min(4, Environment.ProcessorCount))
        };

        Parallel.ForEach(rules, options, rule =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            CleanupCandidate? candidate = ScanRule(rule, cancellationToken);

            if (candidate is not null)
            {
                items.Add(candidate);
            }
        });

        Dictionary<string, int> order = rules
            .Select((rule, index) => (rule.Id, index))
            .ToDictionary(entry => entry.Id, entry => entry.index, StringComparer.OrdinalIgnoreCase);

        List<CleanupCandidate> ordered = items
            .OrderBy(item => order.TryGetValue(item.Rule.Id, out int index) ? index : int.MaxValue)
            .ToList();

        int number = 0;

        foreach (CleanupCandidate item in ordered)
        {
            item.Number = ++number;
        }

        total.Stop();
        _logger.Info($"Cleanup scan finished: {ordered.Count} row(s) in {total.ElapsedMilliseconds} ms.");

        return new CleanupScanResult
        {
            Items = ordered,
            Notes = _catalog.GetDiscoveryNotes(),
            ScannedUtc = DateTime.UtcNow
        };
    }

    private CleanupCandidate? ScanRule(CleanupRule rule, CancellationToken cancellationToken)
    {
        if (rule.Action == CleanupActionKind.EmptyRecycleBin)
        {
            // Already measured once before the parallel pass.
            return null;
        }

        string[] roots = rule.Roots
            .Where(Directory.Exists)
            .Where(root => !_whitelist.IsWhitelisted(root))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (roots.Length == 0)
        {
            return null;
        }

        var stopwatch = Stopwatch.StartNew();
        Measurement measurement = Measure(roots, rule.MinimumAge, cancellationToken);
        stopwatch.Stop();

        if (stopwatch.ElapsedMilliseconds > 2_000)
        {
            _logger.Info($"Cleanup scan: {rule.Id} measured in {stopwatch.ElapsedMilliseconds} ms.");
        }

        if (measurement.Bytes == 0 && measurement.FileCount == 0)
        {
            return null;
        }

        bool running = rule.RequiresAppClosed && IsRunning(rule.ProcessName);

        return new CleanupCandidate
        {
            Rule = rule,
            Roots = roots,
            Bytes = measurement.Bytes,
            FileCount = measurement.FileCount,
            FolderCount = measurement.FolderCount,
            OldestWriteUtc = measurement.Oldest,
            NewestWriteUtc = measurement.Newest,
            IsSelected = rule.Tier == CleanupTier.Safe && !running,
            IsRunning = running
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

            foreach (FileInfo file in SafeFileSystem.EnumerateFiles(root, recurse: true, maxFiles: MaxFilesPerRoot))
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

            folders += CountTopLevelSubfolders(root);
        }

        return new Measurement(bytes, files, folders, oldest, newest);
    }

    private static int CountTopLevelSubfolders(string root)
    {
        try
        {
            return Directory.EnumerateDirectories(root)
                .Take(MaxTopLevelSubfolders)
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
