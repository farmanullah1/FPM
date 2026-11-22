using System.Diagnostics;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Cleanup;

/// <summary>
/// The built-in rule set. Every root list below is fixed and only disposable cache/log
/// folders are named. Application state (profiles, sessions, cookies, settings, models,
/// extensions, databases) is never listed, so no rule can reach it even if it exists.
///
/// Discovery is evidence-based: an application is only included when one of its known data
/// folders exists, and confidence is "Verified" only when a profile marker was found.
/// An application with no supported cache folder is reported, not guessed at.
/// </summary>
public sealed class CleanupRuleCatalog : ICleanupRuleCatalog
{
    private static readonly string LocalAppData =
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    private static readonly string RoamingAppData =
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    private static readonly string UserProfile =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private static readonly string ProgramData =
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

    private static readonly string WindowsFolder =
        Environment.GetFolderPath(Environment.SpecialFolder.Windows);

    private static readonly string TempPath = Path.GetTempPath();

    private static readonly TimeSpan TempMinimumAge = TimeSpan.FromHours(24);

    /// <summary>Junk sub-folders only. These names come from the rule pack, never from disk.</summary>
    private static readonly string[] AiJunkFolders =
    {
        "Cache", "Code Cache", "GPUCache", "DawnGraphiteCache", "DawnWebGPUCache",
        "GrShaderCache", "ShaderCache", "CachedData", "logs"
    };

    private static readonly string[] ChromiumCacheFolders =
    {
        "Cache", "Code Cache", "GPUCache", "GrShaderCache", "ShaderCache",
        "DawnGraphiteCache", "DawnWebGPUCache", "Service Worker\\CacheStorage"
    };

    private readonly Lazy<IReadOnlyList<CleanupRule>> _rules;
    private readonly Lazy<IReadOnlyList<string>> _notes;

    public CleanupRuleCatalog()
    {
        _rules = new Lazy<IReadOnlyList<CleanupRule>>(BuildRules);
        _notes = new Lazy<IReadOnlyList<string>>(BuildNotes);
    }

    public IReadOnlyList<CleanupRule> GetRules() => _rules.Value;

    public IReadOnlyList<string> GetDiscoveryNotes() => _notes.Value;

    private static IReadOnlyList<CleanupRule> BuildRules()
    {
        var rules = new List<CleanupRule>();

        // ------------------------- Windows temp & diagnostics -------------------------
        rules.Add(new CleanupRule
        {
            Id = "windows.user-temp",
            Category = "Windows cleanup",
            Application = "Windows",
            Name = "Your temporary files",
            Description = "Files in your temp folder that have not changed for 24 hours.",
            SafetyNote = "Open files are skipped. Group policy and installer state are not touched.",
            RebuildImpact = "Temporary files are recreated by the programs that need them.",
            Tier = CleanupTier.Safe,
            Roots = new[] { TempPath },
            MinimumAge = TempMinimumAge
        });

        rules.Add(new CleanupRule
        {
            Id = "windows.system-temp",
            Category = "Windows cleanup",
            Application = "Windows",
            Name = "Windows temporary files",
            Description = "The machine-wide %WINDIR%\\Temp folder, 24 hours and older.",
            SafetyNote = "Removing files here usually needs administrator rights; refused files are reported.",
            RebuildImpact = "Recreated on demand.",
            Tier = CleanupTier.Safe,
            Roots = new[] { Path.Combine(WindowsFolder, "Temp") },
            MinimumAge = TempMinimumAge,
            RequiresElevation = true,
            Evidence = WindowsFolder
        });

        rules.Add(new CleanupRule
        {
            Id = "windows.update-cache",
            Category = "Windows cleanup",
            Application = "Windows Update",
            Name = "Windows Update downloads",
            Description = "Downloaded update payloads in SoftwareDistribution\\Download.",
            SafetyNote = "Only the Download folder. Installed updates and update history stay.",
            RebuildImpact = "Windows downloads the payload again when an update needs it.",
            Tier = CleanupTier.Safe,
            Roots = new[] { Path.Combine(WindowsFolder, "SoftwareDistribution", "Download") },
            RequiresElevation = true,
            Evidence = WindowsFolder
        });

        rules.Add(new CleanupRule
        {
            Id = "windows.logs",
            Category = "Windows cleanup",
            Application = "Windows",
            Name = "Windows log files",
            Description = "CBS, DISM and setup logs in %WINDIR%\\Logs.",
            SafetyNote = "Only the Logs folder. Live event logs are stored elsewhere and are untouched.",
            RebuildImpact = "New logs are written as Windows works. Old diagnostics are lost.",
            Tier = CleanupTier.Caution,
            Roots = new[] { Path.Combine(WindowsFolder, "Logs") },
            RequiresElevation = true,
            Evidence = WindowsFolder
        });

        rules.Add(new CleanupRule
        {
            Id = "windows.wer",
            Category = "Logs & crash reports",
            Application = "Windows Error Reporting",
            Name = "Windows error reports",
            Description = "Queued and archived crash reports (WER).",
            SafetyNote = "Only ReportArchive and ReportQueue. No live application data.",
            RebuildImpact = "None. Reports are only kept for diagnostics.",
            Tier = CleanupTier.Safe,
            Roots = new[]
            {
                Path.Combine(ProgramData, "Microsoft", "Windows", "WER", "ReportArchive"),
                Path.Combine(ProgramData, "Microsoft", "Windows", "WER", "ReportQueue"),
                Path.Combine(LocalAppData, "Microsoft", "Windows", "WER", "ReportArchive"),
                Path.Combine(LocalAppData, "Microsoft", "Windows", "WER", "ReportQueue")
            }
        });

        rules.Add(new CleanupRule
        {
            Id = "windows.gpu-cache",
            Category = "Windows cleanup",
            Application = "DirectX / GPU drivers",
            Name = "Shader caches",
            Description = "DirectX and vendor shader caches.",
            SafetyNote = "Only D3DSCache, NVIDIA DX/GLCache and AMD DxCache.",
            RebuildImpact = "Games and GPU apps rebuild shaders, which can slow the first launch.",
            Tier = CleanupTier.Safe,
            Roots = new[]
            {
                Path.Combine(LocalAppData, "D3DSCache"),
                Path.Combine(LocalAppData, "NVIDIA", "DXCache"),
                Path.Combine(LocalAppData, "NVIDIA", "GLCache"),
                Path.Combine(LocalAppData, "AMD", "DxCache")
            }
        });

        rules.Add(new CleanupRule
        {
            Id = "windows.prefetch",
            Category = "Windows cleanup",
            Application = "Windows",
            Name = "Prefetch data",
            Description = "Application launch traces in %WINDIR%\\Prefetch.",
            SafetyNote = "Needs administrator rights. Windows rebuilds the traces.",
            RebuildImpact = "The first launches after cleaning may be slightly slower.",
            Tier = CleanupTier.Caution,
            Roots = new[] { Path.Combine(WindowsFolder, "Prefetch") },
            RequiresElevation = true,
            Evidence = WindowsFolder
        });

        rules.Add(new CleanupRule
        {
            Id = "windows.patchcache",
            Category = "Windows cleanup",
            Application = "Windows Installer",
            Name = "Installer patch cache ($PatchCache$)",
            Description = "Cached MSP patches under %WINDIR%\\Installer\\$PatchCache$.",
            SafetyNote = "Needs administrator rights. Repairing or uninstalling an older update may need the patch again.",
            RebuildImpact = "Windows may have to re-download a patch during repair or uninstall.",
            Tier = CleanupTier.OptIn,
            Roots = new[] { Path.Combine(WindowsFolder, "Installer", "$PatchCache$") },
            RequiresElevation = true,
            Evidence = WindowsFolder
        });

        rules.Add(new CleanupRule
        {
            Id = "windows.recycle-bin",
            Category = "Windows cleanup",
            Application = "Recycle Bin",
            Name = "Recycle Bin",
            Description = "Files already in the Recycle Bin.",
            SafetyNote = "Emptied through the shell API, the same operation as emptying it in Explorer.",
            RebuildImpact = "Deleted files cannot be restored after this.",
            Tier = CleanupTier.Safe,
            Action = CleanupActionKind.EmptyRecycleBin
        });

        // -------------------------------- Browsers ----------------------------------
        AddBrowser(rules, "browser.edge", "Microsoft Edge", "msedge",
            Path.Combine(LocalAppData, "Microsoft", "Edge", "User Data"));

        AddBrowser(rules, "browser.chrome", "Google Chrome", "chrome",
            Path.Combine(LocalAppData, "Google", "Chrome", "User Data"));

        AddBrowser(rules, "browser.brave", "Brave", "brave",
            Path.Combine(LocalAppData, "BraveSoftware", "Brave-Browser", "User Data"));

        string firefoxProfiles = Path.Combine(LocalAppData, "Mozilla", "Firefox", "Profiles");
        string[] firefoxCaches = DiscoverChildFolders(firefoxProfiles, profile => Path.Combine(profile, "cache2"));

        if (firefoxCaches.Length > 0)
        {
            rules.Add(new CleanupRule
            {
                Id = "browser.firefox",
                Category = "Browser & app cache",
                Application = "Mozilla Firefox",
                Name = "Firefox cache (all profiles)",
                Description = "cache2 for every detected Firefox profile.",
                SafetyNote = "Cookies, logins, history and bookmarks live outside cache2 and stay.",
                RebuildImpact = "Pages load from the network again until the cache refills.",
                Tier = CleanupTier.Safe,
                Roots = firefoxCaches,
                RequiresAppClosed = true,
                ProcessName = "firefox",
                Confidence = DetectionConfidence.Verified,
                Evidence = $"{firefoxCaches.Length} profile cache folder(s)"
            });
        }

        // ----------------------------- Developer caches ------------------------------
        string npmCache = Path.Combine(LocalAppData, "npm-cache");
        string npmCacheRoaming = Path.Combine(RoamingAppData, "npm-cache");

        AddDevRule(rules, "dev.npm", "npm", "npm cache",
            "Downloaded npm packages in npm-cache.",
            "The next install downloads whatever a project needs again.",
            CleanupTier.Safe,
            new[] { npmCache, npmCacheRoaming },
            marker: "_cacache");

        string nugetV3 = Path.Combine(LocalAppData, "NuGet", "v3-cache");

        AddDevRule(rules, "dev.nuget", "NuGet", "NuGet caches",
            "NuGet HTTP and plugin caches plus NuGetScratch; no global packages folder.",
            "The next restore downloads packages again.",
            CleanupTier.Safe,
            new[]
            {
                nugetV3,
                Path.Combine(LocalAppData, "NuGet", "plugins-cache"),
                Path.Combine(TempPath, "NuGetScratch")
            },
            marker: "v3-cache");

        AddDevRule(rules, "dev.pip", "pip", "pip cache",
            "pip's HTTP and wheel cache.",
            "The next install downloads the wheels again.",
            CleanupTier.Safe,
            new[] { Path.Combine(LocalAppData, "pip", "Cache") },
            marker: "http-v2");

        AddDevRule(rules, "dev.yarn", "Yarn", "Yarn cache",
            "Yarn Classic and Berry download caches.",
            "The next install downloads packages again. Project node_modules folders are never touched.",
            CleanupTier.Caution,
            new[]
            {
                Path.Combine(LocalAppData, "Yarn", "Cache"),
                Path.Combine(LocalAppData, "Yarn", "Berry", "cache")
            },
            marker: "Cache");

        AddDevRule(rules, "dev.pnpm", "pnpm", "pnpm store",
            "pnpm's content-addressable store.",
            "Existing projects keep working, but a future install may need to relink or re-download packages.",
            CleanupTier.Caution,
            new[]
            {
                Path.Combine(LocalAppData, "pnpm", "store"),
                Path.Combine(LocalAppData, "pnpm-store")
            },
            marker: "store");

        // ------------------------------ AI IDEs & agents -----------------------------
        AddAiApp(rules, "ai.antigravity", "Google Antigravity", new[]
        {
            Path.Combine(RoamingAppData, "Antigravity"),
            Path.Combine(UserProfile, ".antigravity-ide"),
            Path.Combine(UserProfile, ".antigravity")
        });

        AddAiApp(rules, "ai.freebuff", "Freebuff", new[]
        {
            Path.Combine(RoamingAppData, "Freebuff"),
            Path.Combine(UserProfile, ".freebuff")
        });

        AddAiApp(rules, "ai.cursor", "Cursor", new[]
        {
            Path.Combine(RoamingAppData, "Cursor"),
            Path.Combine(UserProfile, ".cursor")
        });

        AddAiApp(rules, "ai.vscode", "Visual Studio Code", new[]
        {
            Path.Combine(RoamingAppData, "Code"),
            Path.Combine(RoamingAppData, "Code - Insiders")
        });

        AddAiApp(rules, "ai.windsurf", "Windsurf", new[]
        {
            Path.Combine(RoamingAppData, "Windsurf"),
            Path.Combine(UserProfile, ".windsurf")
        });

        return rules;
    }

    private static void AddBrowser(List<CleanupRule> rules, string id, string name, string processName, string userData)
    {
        string[] profiles = DiscoverChildFolders(userData, profile =>
        {
            string folder = Path.GetFileName(profile);

            if (!folder.Equals("Default", StringComparison.OrdinalIgnoreCase) &&
                !folder.Equals("Guest Profile", StringComparison.OrdinalIgnoreCase) &&
                !folder.Equals("System Profile", StringComparison.OrdinalIgnoreCase) &&
                !folder.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return profile;
        });

        if (profiles.Length == 0)
        {
            return;
        }

        var roots = new List<string>();

        foreach (string profile in profiles)
        {
            foreach (string cacheName in ChromiumCacheFolders)
            {
                string cache = Path.Combine(profile, cacheName);

                if (Directory.Exists(cache))
                {
                    roots.Add(cache);
                }
            }
        }

        if (roots.Count == 0)
        {
            return;
        }

        rules.Add(new CleanupRule
        {
            Id = id,
            Category = "Browser & app cache",
            Application = name,
            Name = $"{name} cache (all profiles)",
            Description = "HTTP, code and GPU caches for every detected profile.",
            SafetyNote = "Cookies, passwords, history, bookmarks, extensions and sessions are never touched.",
            RebuildImpact = "Sites load from the network again until the cache refills.",
            Tier = CleanupTier.Safe,
            Roots = roots.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            RequiresAppClosed = true,
            ProcessName = processName,
            Confidence = DetectionConfidence.Verified,
            Evidence = $"{profiles.Length} profile(s), {roots.Count} cache folder(s)"
        });
    }

    private static void AddDevRule(
        List<CleanupRule> rules,
        string id,
        string application,
        string name,
        string description,
        string rebuild,
        CleanupTier tier,
        string[] roots,
        string marker)
    {
        string[] existing = roots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        if (existing.Length == 0)
        {
            return;
        }

        bool verified = existing.Any(root => Directory.Exists(Path.Combine(root, marker)));

        rules.Add(new CleanupRule
        {
            Id = id,
            Category = "Developer cache",
            Application = application,
            Name = name,
            Description = description,
            SafetyNote = "Only these fixed cache folders. Project files, node_modules, global packages and environment folders are never scanned.",
            RebuildImpact = rebuild,
            Tier = tier,
            Roots = existing,
            Confidence = verified ? DetectionConfidence.Verified : DetectionConfidence.Probable,
            Evidence = existing[0]
        });
    }

    private static void AddAiApp(List<CleanupRule> rules, string id, string application, string[] knownRoots)
    {
        string[] roots = knownRoots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        if (roots.Length == 0)
        {
            return;
        }

        var caches = new List<string>();

        foreach (string root in roots)
        {
            foreach (string junk in AiJunkFolders)
            {
                string candidate = Path.Combine(root, junk);

                if (Directory.Exists(candidate))
                {
                    caches.Add(candidate);
                }
            }
        }

        if (caches.Count == 0)
        {
            return;
        }

        // A profile marker is verified evidence that the folder belongs to this application.
        bool verified = roots.Any(root =>
            File.Exists(Path.Combine(root, "Local State")) ||
            File.Exists(Path.Combine(root, "Preferences")));

        rules.Add(new CleanupRule
        {
            Id = id,
            Category = "AI & IDE cleanup",
            Application = application,
            Name = $"{application} caches",
            Description = "Cache, code cache, GPU/shader caches and logs inside the detected application data folder.",
            SafetyNote = "Partitions, Local Storage, Session Storage, IndexedDB, Network state, settings, extensions, chat history and models are never touched.",
            RebuildImpact = "The application rebuilds caches on its next launch; the first start may be slower.",
            Tier = CleanupTier.Safe,
            Roots = caches.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            Confidence = verified ? DetectionConfidence.Verified : DetectionConfidence.Probable,
            Evidence = verified
                ? $"profile marker in {roots[0]}"
                : $"known application data folder {roots[0]}"
        });
    }

    private static string[] DiscoverChildFolders(string parent, Func<string, string?> select)
    {
        try
        {
            if (!Directory.Exists(parent))
            {
                return Array.Empty<string>();
            }

            return Directory.GetDirectories(parent)
                .Select(select)
                .Where(path => path is not null && Directory.Exists(path))
                .Select(path => path!)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static IReadOnlyList<string> BuildNotes()
    {
        var notes = new List<string>();

        // Omnirush was detected on this machine but has no documented disposable cache folder.
        // Reporting that honestly is better than guessing at its state folders.
        string[] omnirushRoots =
        {
            Path.Combine(RoamingAppData, "omnirush"),
            Path.Combine(RoamingAppData, "Omnirush"),
            Path.Combine(UserProfile, ".omnirush")
        };

        string[] existing = omnirushRoots.Where(Directory.Exists).ToArray();

        if (existing.Length > 0)
        {
            bool hasKnownCache = existing.Any(root => AiJunkFolders.Any(junk => Directory.Exists(Path.Combine(root, junk))));

            if (!hasKnownCache)
            {
                notes.Add("Omnirush: no supported cleanup target found. Its state folders were left untouched.");
            }
        }

        if (Debugger.IsAttached)
        {
            notes.Add("Debug build: cleanup log is %LOCALAPPDATA%\\FarmanullahPCManager\\logs\\cleanup_log.txt.");
        }

        return notes;
    }
}
