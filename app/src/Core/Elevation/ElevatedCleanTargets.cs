namespace PCManager.Core.Elevation;

/// <summary>
/// Fixed machine-wide cleanup targets. Both the rule catalog and the elevated helper read this
/// list, so the client and the helper can never disagree about a path. The helper accepts only
/// these rule ids — never a path.
/// </summary>
public sealed record ElevatedCleanTarget(
    string RuleId,
    string DisplayName,
    IReadOnlyList<string> Roots,
    TimeSpan MinimumAge,
    bool StopUpdateServices = false);

public static class ElevatedCleanTargets
{
    private static readonly string WindowsFolder =
        Environment.GetFolderPath(Environment.SpecialFolder.Windows);

    private static readonly ElevatedCleanTarget[] Targets =
    {
        new(
            "windows.system-temp",
            "Windows temporary files",
            new[] { Path.Combine(WindowsFolder, "Temp") },
            TimeSpan.FromHours(24)),

        new(
            "windows.update-cache",
            "Windows Update downloads",
            new[] { Path.Combine(WindowsFolder, "SoftwareDistribution", "Download") },
            TimeSpan.Zero,
            StopUpdateServices: true),

        new(
            "windows.logs",
            "Windows log files",
            new[] { Path.Combine(WindowsFolder, "Logs") },
            TimeSpan.Zero),

        new(
            "windows.prefetch",
            "Prefetch data",
            new[] { Path.Combine(WindowsFolder, "Prefetch") },
            TimeSpan.Zero),

        new(
            "windows.patchcache",
            "Installer patch cache ($PatchCache$)",
            new[] { Path.Combine(WindowsFolder, "Installer", "$PatchCache$") },
            TimeSpan.Zero)
    };

    public static IReadOnlyList<ElevatedCleanTarget> All => Targets;

    public static bool TryGet(string? ruleId, out ElevatedCleanTarget target)
    {
        target = Targets.FirstOrDefault(
                     candidate => candidate.RuleId.Equals(ruleId, StringComparison.OrdinalIgnoreCase))
                 ?? Targets[0];

        return Targets.Any(candidate =>
            candidate.RuleId.Equals(ruleId, StringComparison.OrdinalIgnoreCase));
    }

    public static ElevatedCleanTarget Get(string ruleId)
        => TryGet(ruleId, out ElevatedCleanTarget target)
            ? target
            : throw new ArgumentOutOfRangeException(nameof(ruleId), ruleId, "Unknown elevated cleanup rule.");
}
