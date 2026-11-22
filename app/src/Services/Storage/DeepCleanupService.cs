using PCManager.Core.Formatting;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;
using PCManager.Services.Infrastructure;

namespace PCManager.Services.Storage;

/// <summary>
/// Deep cleanup across five independent categories. Each category is measured on its own
/// so the UI can show real byte counts before anything is removed.
///
/// Categories that need administrator rights (Windows Update downloads, the machine-wide
/// log folder) are still measured; when deletion is denied the category is reported as
/// skipped instead of failing the run.
/// </summary>
public sealed class DeepCleanupService : IDeepCleanupService
{
    private readonly ITempFileCleaner _tempCleaner;
    private readonly IBrowserCacheCleaner _cacheCleaner;
    private readonly IRecycleBinService _recycleBin;

    public DeepCleanupService(
        ITempFileCleaner tempCleaner,
        IBrowserCacheCleaner cacheCleaner,
        IRecycleBinService recycleBin)
    {
        _tempCleaner = tempCleaner;
        _cacheCleaner = cacheCleaner;
        _recycleBin = recycleBin;
    }

    /// <summary>%SystemRoot%\SoftwareDistribution\Download — update payload cache.</summary>
    private static string UpdateCache => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        "SoftwareDistribution", "Download");

    private static string SystemLogs => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        "Logs");

    private static string WindowsTemp => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        "Temp");

    public Task<IReadOnlyList<CleanupCategory>> ScanAsync(CancellationToken cancellationToken = default) => Task.Run<IReadOnlyList<CleanupCategory>>(() =>
    {
        var categories = new List<CleanupCategory>();

        cancellationToken.ThrowIfCancellationRequested();

        categories.Add(new CleanupCategory
        {
            Id = "updates",
            Title = "Windows Update cache",
            Description = "Downloaded update payloads in SoftwareDistribution\\Download. Windows re-downloads them if needed.",
            Bytes = FolderCleaner.Measure(UpdateCache)
        });

        categories.Add(new CleanupCategory
        {
            Id = "logs",
            Title = "System logs and Windows Temp",
            Description = "CBS, DISM and setup logs plus %WINDIR%\\Temp. Removing them needs administrator rights.",
            Bytes = FolderCleaner.Measure(SystemLogs, WindowsTemp)
        });

        categories.Add(new CleanupCategory
        {
            Id = "recycle",
            Title = "Recycle Bin",
            Description = "Files you already deleted. Emptying it frees the space immediately.",
            Bytes = _recycleBin.GetSize()
        });

        categories.Add(new CleanupCategory
        {
            Id = "temp",
            Title = "Temporary files",
            Description = "Files in the user temp folders that have not been touched for 48 hours.",
            Bytes = _tempCleaner.GetReclaimableBytes()
        });

        categories.Add(new CleanupCategory
        {
            Id = "browser",
            Title = "Browser caches",
            Description = "Edge, Chrome and Brave disk caches. Cookies, history and passwords are never touched.",
            Bytes = _cacheCleaner.GetReclaimableBytes()
        });

        return categories;
    }, cancellationToken);

    public Task<CleanupOutcome> CleanAsync(IEnumerable<CleanupCategory> categories, CancellationToken cancellationToken = default)
        => Task.Run(() =>
        {
            long freed = 0;
            int cleaned = 0;
            int skipped = 0;

            foreach (CleanupCategory category in categories.Where(c => c.IsSelected && !c.IsCleaned))
            {
                cancellationToken.ThrowIfCancellationRequested();

                long before = category.Bytes;
                long released;

                switch (category.Id)
                {
                    case "updates":
                        released = FolderCleaner.DeleteContents(UpdateCache).Freed;
                        category.Bytes = Math.Max(0, before - released);
                        break;

                    case "logs":
                        released = FolderCleaner.DeleteContents(SystemLogs, WindowsTemp).Freed;
                        category.Bytes = Math.Max(0, before - released);
                        break;

                    case "recycle":
                        released = _recycleBin.Empty();
                        category.Bytes = 0;
                        break;

                    case "temp":
                        released = _tempCleaner.Clean();
                        category.Bytes = Math.Max(0, before - released);
                        break;

                    case "browser":
                        released = _cacheCleaner.Clean();
                        category.Bytes = Math.Max(0, before - released);
                        break;

                    default:
                        continue;
                }

                freed += released;

                // A category that had bytes but released none was blocked: report it as
                // skipped rather than pretending it was cleaned.
                if (released > 0)
                {
                    cleaned++;
                    category.IsCleaned = true;
                }
                else if (before > 0)
                {
                    skipped++;
                }
                else
                {
                    category.IsCleaned = true;
                }
            }

            string message = freed > 0
                ? $"{ByteFormatter.Format(freed)} released."
                : skipped > 0
                    ? "Nothing could be removed. Some categories need administrator rights."
                    : "Everything selected was already clean.";

            return new CleanupOutcome(freed, cleaned, skipped, message);
        }, cancellationToken);
}
