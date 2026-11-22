using PCManager.Services.Abstractions;

namespace PCManager.Services.Cleanup;

/// <summary>
/// Hard runtime guard. A cleanup rule's roots are the only places anything may be removed
/// from; on top of that this refuses drive roots, Windows, Program Files, document folders,
/// credential-like names, reparse points and anything the user whitelisted.
///
/// It is deliberately path-only and static-data driven: no rule, JSON file or UI text can
/// add a root at runtime. Failing closed is the point — an unknown path is refused.
/// </summary>
public sealed class SafetyGuard : ISafetyGuard
{
    private static readonly string UserProfile =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private static readonly string LocalAppData =
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    private static readonly string RoamingAppData =
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    private static readonly string ProgramData =
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

    private static readonly string WindowsFolder =
        Environment.GetFolderPath(Environment.SpecialFolder.Windows);

    private static readonly string ProgramFiles =
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

    private static readonly string ProgramFilesX86 =
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

    private static readonly string SystemDrive = Path.GetPathRoot(WindowsFolder) ?? @"C:\";

    /// <summary>Whole trees that may never be a cleanup target (named exceptions below).</summary>
    private static readonly (string Path, string Label)[] DeniedSubtrees = BuildDeniedSubtrees();

    /// <summary>Container folders that may never themselves be a cleanup target.</summary>
    private static readonly (string Path, string Label)[] DeniedExactPaths = BuildDeniedExactPaths();

    /// <summary>Folder or file names that always mean "private state", never "cache".</summary>
    private static readonly string[] DeniedSegmentNames =
    {
        "cookies", "login data", "history", "bookmarks", "web data", "preferences",
        "local state", "local storage", "session storage", "indexeddb", "extensions",
        "node_modules", ".git", ".ssh", ".aws", ".gitconfig", "partitions",
        "blob_storage", "globalstorage", "workspacestorage", "mcp", "models"
    };

    /// <summary>Name fragments that indicate credentials or application databases.</summary>
    private static readonly string[] DeniedNameFragments =
    {
        "token", "credential", "secret", "apikey", "api_key", "password", ".env",
        ".sqlite", ".db", ".vscdb", "auth"
    };

    private readonly IWhitelistService _whitelist;

    public SafetyGuard(IWhitelistService whitelist) => _whitelist = whitelist;

    public bool IsWhitelisted(string path) => _whitelist.IsWhitelisted(path);

    public SafetyVerdict Evaluate(string path, IReadOnlyList<string> allowedRoots)
        => EvaluateCore(path, allowedRoots, allowRootItself: false);

    public SafetyVerdict EvaluateRoot(string path, IReadOnlyList<string> allowedRoots)
        => EvaluateCore(path, allowedRoots, allowRootItself: true);

    private SafetyVerdict EvaluateCore(string path, IReadOnlyList<string> allowedRoots, bool allowRootItself)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return SafetyVerdict.Deny("The path is empty.");
        }

        string full;

        try
        {
            full = Path.GetFullPath(path);
        }
        catch
        {
            return SafetyVerdict.Deny("The path is not a valid file-system path.");
        }

        if (full.Length > 32_000)
        {
            return SafetyVerdict.Deny("The path is too long.");
        }

        if (_whitelist.IsWhitelisted(full))
        {
            return SafetyVerdict.Deny("This path is on your whitelist.");
        }

        if (HasDeniedName(full))
        {
            return SafetyVerdict.Deny("The name looks like settings, credentials or a database.");
        }

        string? matchedRoot = allowedRoots
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Select(TryFullPath)
            .Where(root => root is not null)
            .FirstOrDefault(root => IsSameOrUnder(full, root!));

        if (matchedRoot is null)
        {
            return SafetyVerdict.Deny("The path is outside the folder this rule is allowed to touch.");
        }

        if (HasReparsePoint(full, matchedRoot))
        {
            return SafetyVerdict.Deny("The path or a parent is a junction or symlink; it will not be followed.");
        }

        // Windows Temp/Logs/SoftwareDistribution, Edge profile caches and WER are named
        // exceptions to the protected trees; everything else in those trees is refused.
        if (IsDeniedPath(full) && !IsNamedAllowedTarget(full))
        {
            return SafetyVerdict.Deny("The folder is protected by the hard deny-list.");
        }

        if (!allowRootItself && PathsEqual(full, matchedRoot))
        {
            return SafetyVerdict.Deny("A rule root itself is never deleted, only its contents.");
        }

        return SafetyVerdict.Allow();
    }

    private static (string Path, string Label)[] BuildDeniedSubtrees()
    {
        var trees = new List<(string, string)>
        {
            (WindowsFolder, "the Windows folder"),
            (ProgramFiles, "Program Files"),
            (ProgramFilesX86, "Program Files (x86)"),
            (Path.Combine(LocalAppData, "Programs"), "installed applications"),
            (Path.Combine(LocalAppData, "Packages"), "Store application state"),
            (Path.Combine(LocalAppData, "FarmanullahPCManager"), "this app's own data"),
            (Path.Combine(LocalAppData, "Microsoft"), "Microsoft application state"),
            (Path.Combine(RoamingAppData, "Microsoft"), "Microsoft application state"),
            (Path.Combine(ProgramData, "Microsoft"), "Microsoft application state"),
            (Path.Combine(UserProfile, "Desktop"), "the Desktop"),
            (Path.Combine(UserProfile, "Documents"), "Documents"),
            (Path.Combine(UserProfile, "Downloads"), "Downloads"),
            (Path.Combine(UserProfile, "Pictures"), "Pictures"),
            (Path.Combine(UserProfile, "Videos"), "Videos"),
            (Path.Combine(UserProfile, "Music"), "Music"),
            (Path.Combine(UserProfile, ".ssh"), "SSH keys"),
            (Path.Combine(UserProfile, ".aws"), "AWS credentials"),
            (Path.Combine(SystemDrive, "$Recycle.Bin"), "the Recycle Bin folder"),
            (Path.Combine(SystemDrive, "System Volume Information"), "system volume data")
        };

        return trees
            .Where(tree => !string.IsNullOrWhiteSpace(tree.Item1))
            .Select(tree => (Path.TrimEndingDirectorySeparator(tree.Item1), tree.Item2))
            .ToArray();
    }

    private static (string Path, string Label)[] BuildDeniedExactPaths()
    {
        var exact = new List<(string, string)>
        {
            (SystemDrive, "a drive root"),
            (WindowsFolder, "the Windows folder"),
            (ProgramFiles, "Program Files"),
            (ProgramFilesX86, "Program Files (x86)"),
            (UserProfile, "your user profile root"),
            (LocalAppData, "the LocalAppData root"),
            (RoamingAppData, "the RoamingAppData root"),
            (ProgramData, "the ProgramData root")
        };

        return exact
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Item1))
            .Select(entry => (Path.TrimEndingDirectorySeparator(entry.Item1), entry.Item2))
            .ToArray();
    }

    private static string? TryFullPath(string path)
    {
        try { return Path.GetFullPath(path); }
        catch { return null; }
    }

    private static bool IsDeniedPath(string full)
    {
        foreach ((string root, _) in DeniedExactPaths)
        {
            if (PathsEqual(full, root))
            {
                return true;
            }
        }

        foreach ((string root, _) in DeniedSubtrees)
        {
            if (IsSameOrUnder(full, root))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNamedAllowedTarget(string full)
    {
        // Windows: only these exact subtrees, all of which the catalog uses.
        string[] windowsTargets =
        {
            Path.Combine(WindowsFolder, "Temp"),
            Path.Combine(WindowsFolder, "Logs"),
            Path.Combine(WindowsFolder, "SoftwareDistribution", "Download"),
            Path.Combine(WindowsFolder, "Installer", "$PatchCache$"),
            Path.Combine(WindowsFolder, "Prefetch")
        };

        if (windowsTargets.Any(target => IsSameOrUnder(full, target)))
        {
            return true;
        }

        if (IsSameOrUnder(full, Path.Combine(LocalAppData, "Microsoft", "Windows", "WER")))
        {
            return true;
        }

        if (IsSameOrUnder(full, Path.Combine(ProgramData, "Microsoft", "Windows", "WER")))
        {
            return true;
        }

        // Edge is the one Microsoft-owned browser cache exception: only its User Data tree,
        // and the catalog only ever points at Cache / Code Cache / GPU cache folders there.
        return IsSameOrUnder(full, Path.Combine(LocalAppData, "Microsoft", "Edge", "User Data"));
    }

    private static bool HasDeniedName(string full)
    {
        string[] segments = full.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);

        foreach (string segment in segments)
        {
            string value = segment.Trim().ToLowerInvariant();

            if (DeniedSegmentNames.Contains(value))
            {
                return true;
            }

            if (DeniedNameFragments.Any(fragment => value.Contains(fragment, StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasReparsePoint(string path, string allowedRoot)
    {
        string? current = path;

        while (!string.IsNullOrWhiteSpace(current))
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    return true;
                }
            }
            catch
            {
                // The item vanished or is inaccessible; deletion will skip it.
                return false;
            }

            if (PathsEqual(current, allowedRoot))
            {
                break;
            }

            current = Path.GetDirectoryName(current);
        }

        return false;
    }

    private static bool PathsEqual(string left, string right)
        => Path.TrimEndingDirectorySeparator(left)
            .Equals(Path.TrimEndingDirectorySeparator(right), StringComparison.OrdinalIgnoreCase);

    internal static bool IsSameOrUnder(string path, string root)
    {
        string candidate = Path.TrimEndingDirectorySeparator(path);
        string parent = Path.TrimEndingDirectorySeparator(root);

        if (candidate.Equals(parent, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string prefix = parent.EndsWith(Path.DirectorySeparatorChar)
            ? parent
            : parent + Path.DirectorySeparatorChar;

        return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
