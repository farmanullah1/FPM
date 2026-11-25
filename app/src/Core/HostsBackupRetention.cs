using System.Text.RegularExpressions;

namespace PCManager.Core;

/// <summary>
/// Keeps the elevated helper's hosts backup folder bounded. Only the timestamped backups the
/// helper itself writes are ever candidates for removal; anything else in the folder is left
/// alone. Names sort chronologically because the timestamp has a fixed width.
/// </summary>
public static class HostsBackupRetention
{
    public const int DefaultKeep = 20;

    public const string SearchPattern = "hosts-*.hosts";

    private static readonly Regex BackupNamePattern = new(
        @"^hosts-\d{8}-\d{6}\.hosts$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>The supplied backups that fall outside the newest <paramref name="keep"/>.</summary>
    public static IReadOnlyList<string> SelectObsolete(IEnumerable<string> paths, int keep)
    {
        int retain = Math.Max(1, keep);

        return paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Where(path => IsBackupName(Path.GetFileName(path)))
            .OrderByDescending(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .Skip(retain)
            .ToList();
    }

    public static bool IsBackupName(string? fileName)
        => !string.IsNullOrEmpty(fileName) && BackupNamePattern.IsMatch(fileName);
}
