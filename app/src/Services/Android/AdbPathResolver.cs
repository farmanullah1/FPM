using PCManager.Core;

namespace PCManager.Services.Android;

public sealed record AdbSearchPaths(
    string LocalToolsDirectory,
    IReadOnlyList<string> CommonDirectories,
    IReadOnlyList<string> PathDirectories)
{
    public static AdbSearchPaths FromEnvironment()
    {
        string local = Path.Combine(AppInfo.DataFolder, "tools", "platform-tools");
        var common = new List<string>();
        AddSdk(common, Environment.GetEnvironmentVariable("ANDROID_HOME"));
        AddSdk(common, Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT"));
        AddSdk(common, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Android", "Sdk"));
        AddSdk(common, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Android"));

        string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        string[] pathDirectories = path.Split(
            Path.PathSeparator,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new AdbSearchPaths(local, common, pathDirectories);
    }

    private static void AddSdk(List<string> directories, string? sdkRoot)
    {
        if (!string.IsNullOrWhiteSpace(sdkRoot))
        {
            directories.Add(Path.Combine(sdkRoot, "platform-tools"));
        }
    }
}

public sealed record AdbPathCandidate(string Path, string Source);

public static class AdbPathResolver
{
    public static IReadOnlyList<AdbPathCandidate> Resolve(
        string? configuredPath,
        AdbSearchPaths paths,
        Func<string, bool>? exists = null)
    {
        Func<string, bool> fileExists = exists ?? File.Exists;
        var candidates = new List<AdbPathCandidate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Add(candidates, seen, configuredPath, "configured");
        Add(candidates, seen, paths.LocalToolsDirectory, "FPM");
        foreach (string directory in paths.CommonDirectories)
        {
            Add(candidates, seen, directory, "Android SDK");
        }

        foreach (string directory in paths.PathDirectories)
        {
            Add(candidates, seen, directory, "PATH");
        }

        return candidates
            .Where(candidate => fileExists(candidate.Path))
            .ToList();
    }

    private static void Add(
        List<AdbPathCandidate> candidates,
        HashSet<string> seen,
        string? value,
        string source)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        string path = Environment.ExpandEnvironmentVariables(value.Trim().Trim('"'));

        try
        {
            if (!path.EndsWith("adb.exe", StringComparison.OrdinalIgnoreCase))
            {
                path = Path.Combine(path, "adb.exe");
            }

            path = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return;
        }

        if (seen.Add(path))
        {
            candidates.Add(new AdbPathCandidate(path, source));
        }
    }
}
