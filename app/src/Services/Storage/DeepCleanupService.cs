using System.Diagnostics;
using PCManager.Core.Formatting;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;
using PCManager.Services.Infrastructure;

namespace PCManager.Services.Storage;

/// <summary>
/// Deep cleanup across independent categories. Each category is measured on its own so the UI
/// can show real byte counts before anything is removed.
///
/// Everything here is a fixed root list: no user pattern, folder or wildcard is ever passed in,
/// so a category cannot be widened at runtime. Categories that need administrator rights
/// (Windows Update downloads, the machine-wide log folder, the installer patch cache) are still
/// measured; when deletion is denied the category is reported as skipped instead of failing the
/// run. Browser caches are measured even while the browser runs but are not deleted in that
/// case, because a running browser holds its cache files open.
/// </summary>
public sealed class DeepCleanupService : IDeepCleanupService
{
    private readonly ITempFileCleaner _tempCleaner;
    private readonly IBrowserCacheCleaner _cacheCleaner;
    private readonly IRecycleBinService _recycleBin;

    private static readonly string LocalAppData =
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    private static readonly string RoamingAppData =
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    private static readonly string WindowsFolder =
        Environment.GetFolderPath(Environment.SpecialFolder.Windows);

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
    private static string UpdateCache => Path.Combine(WindowsFolder, "SoftwareDistribution", "Download");

    private static string SystemLogs => Path.Combine(WindowsFolder, "Logs");

    private static string WindowsTemp => Path.Combine(WindowsFolder, "Temp");

    /// <summary>%SystemRoot%\Installer\$PatchCache$ — cached MSP patches for installed updates.</summary>
    private static string PatchCache => Path.Combine(WindowsFolder, "Installer", "$PatchCache$");

    /// <summary>npm keeps its content-addressed cache here (old npm used %APPDATA%).</summary>
    private static readonly string[] NpmRoots =
    {
        Path.Combine(LocalAppData, "npm-cache"),
        Path.Combine(RoamingAppData, "npm-cache")
    };

    /// <summary>NuGet's http cache, plugin cache and per-user scratch folder.</summary>
    private static readonly string[] NuGetRoots =
    {
        Path.Combine(LocalAppData, "NuGet", "v3-cache"),
        Path.Combine(LocalAppData, "NuGet", "plugins-cache"),
        Path.Combine(Path.GetTempPath(), "NuGetScratch")
    };

    /// <summary>Download caches of the package managers most often found on a dev machine.</summary>
    private static readonly string[] PackageManagerRoots =
    {
        Path.Combine(LocalAppData, "pip", "Cache"),
        Path.Combine(LocalAppData, "Yarn", "Cache"),
        Path.Combine(LocalAppData, "Yarn", "Berry", "cache"),
        Path.Combine(LocalAppData, "pnpm", "store"),
        Path.Combine(LocalAppData, "pnpm-store")
    };

    /// <summary>Every Firefox profile's cache2 folder, discovered at scan time.</summary>
    private static string[] FirefoxCacheRoots()
    {
        string profiles = Path.Combine(LocalAppData, "Mozilla", "Firefox", "Profiles");

        try
        {
            return Directory.Exists(profiles)
                ? Directory.GetDirectories(profiles)
                    .Select(profile => Path.Combine(profile, "cache2"))
                    .Where(Directory.Exists)
                    .ToArray()
                : Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static bool IsRunning(string processName)
    {
        try
        {
            return Process.GetProcessesByName(processName).Length > 0;
        }
        catch
        {
            return false;
        }
    }

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
            Id = "patchcache",
            Title = "Installer patch cache ($PatchCache$)",
            Description = "Cached patches in %WINDIR%\\Installer\\$PatchCache$. Needs administrator rights; repairing or uninstalling an older update may have to re-download the patch.",
            Bytes = FolderCleaner.Measure(PatchCache)
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

        categories.Add(new CleanupCategory
        {
            Id = "firefox",
            Title = "Firefox cache",
            Description = "cache2 for every Firefox profile. Cookies, history and logins are untouched; the category is skipped while Firefox is running.",
            Bytes = FolderCleaner.Measure(FirefoxCacheRoots())
        });

        categories.Add(new CleanupCategory
        {
            Id = "npm",
            Title = "npm cache",
            Description = "Downloaded packages in %LOCALAPPDATA%\\npm-cache. npm re-downloads whatever a project needs on the next install.",
            Bytes = FolderCleaner.Measure(NpmRoots)
        });

        categories.Add(new CleanupCategory
        {
            Id = "nuget",
            Title = "NuGet caches",
            Description = "NuGet http and plugin caches plus the NuGetScratch folder. Restored again by the next build.",
            Bytes = FolderCleaner.Measure(NuGetRoots)
        });

        categories.Add(new CleanupCategory
        {
            Id = "packagemanagers",
            Title = "Package manager stores",
            Description = "pip, Yarn and pnpm download caches. Rebuilt on the next install; no project files are touched.",
            Bytes = FolderCleaner.Measure(PackageManagerRoots)
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

                    case "patchcache":
                        released = FolderCleaner.DeleteContents(PatchCache).Freed;
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

                    case "firefox":
                        // A running Firefox keeps cache2 entries mapped; deleting them would
                        // either fail per file or corrupt the cache index, so do nothing.
                        released = IsRunning("firefox")
                            ? 0
                            : FolderCleaner.DeleteContents(FirefoxCacheRoots()).Freed;
                        category.Bytes = Math.Max(0, before - released);
                        break;

                    case "npm":
                        released = FolderCleaner.DeleteContents(NpmRoots).Freed;
                        category.Bytes = Math.Max(0, before - released);
                        break;

                    case "nuget":
                        released = FolderCleaner.DeleteContents(NuGetRoots).Freed;
                        category.Bytes = Math.Max(0, before - released);
                        break;

                    case "packagemanagers":
                        released = FolderCleaner.DeleteContents(PackageManagerRoots).Freed;
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
                    ? "Nothing could be removed. Some categories need administrator rights, or the owning app is running."
                    : "Everything selected was already clean.";

            return new CleanupOutcome(freed, cleaned, skipped, message);
        }, cancellationToken);
}
