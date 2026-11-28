namespace PCManager.Core.Debloat;

/// <summary>Parsed --debloat options. Kept in Core so the CLI rules are testable without WPF.</summary>
public sealed record DebloatCommandOptions(
    bool DryRun,
    bool Silent,
    bool AcceptRisk,
    string? Preset,
    IReadOnlyList<string> Tweaks,
    IReadOnlyList<string> Apps,
    string User);

public sealed record DebloatCommandParse(DebloatCommandOptions? Options, string? Error)
{
    public bool Success => Options is not null;
}

public static class DebloatCommandLine
{
    public static bool IsDebloatCommand(string[] args) =>
        args.Any(arg => arg.Equals("--debloat", StringComparison.OrdinalIgnoreCase));

    public static DebloatCommandParse Parse(string[] args)
    {
        bool dryRun = false;
        bool silent = false;
        bool acceptRisk = false;
        string? preset = null;
        string user = "current";
        var tweaks = new List<string>();
        var apps = new List<string>();

        for (int index = 0; index < args.Length; index++)
        {
            string arg = args[index];

            switch (arg.ToLowerInvariant())
            {
                case "--debloat":
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--silent":
                    silent = true;
                    break;
                case "--accept-risk":
                    acceptRisk = true;
                    break;
                case "--preset":
                    if (!TryValue(args, ref index, out preset))
                    {
                        return Fail("--preset needs a preset name.");
                    }

                    break;
                case "--tweaks":
                    if (!TryValue(args, ref index, out string? tweakValue))
                    {
                        return Fail("--tweaks needs a comma-separated id list.");
                    }

                    tweaks.AddRange(Split(tweakValue));
                    break;
                case "--apps":
                    if (!TryValue(args, ref index, out string? appValue))
                    {
                        return Fail("--apps needs a comma-separated id list.");
                    }

                    apps.AddRange(Split(appValue));
                    break;
                case "--user":
                    if (!TryValue(args, ref index, out string? userValue))
                    {
                        return Fail("--user needs a profile name or SID.");
                    }

                    user = userValue;
                    break;
                default:
                    return Fail($"Unknown debloat option '{arg}'.");
            }
        }

        if (silent && !acceptRisk)
        {
            return Fail("--silent requires --accept-risk. Nothing was changed.");
        }

        if (preset is null && tweaks.Count == 0 && apps.Count == 0)
        {
            return Fail("Choose --preset, --tweaks or --apps.");
        }

        string[] presetIds = { "recommended", "privacy", "performance", "minimal", "custom" };

        if (preset is not null && !presetIds.Contains(preset, StringComparer.OrdinalIgnoreCase))
        {
            return Fail($"Unknown preset '{preset}'. Use {string.Join(", ", presetIds)}.");
        }

        if (preset is null && tweaks.Count == 0)
        {
            return Fail("App removal is not enabled; --apps can only accompany a tweak selection.");
        }

        return new DebloatCommandParse(
            new DebloatCommandOptions(
                dryRun,
                silent,
                acceptRisk,
                preset?.ToLowerInvariant(),
                tweaks.Distinct(StringComparer.Ordinal).ToList(),
                apps.Distinct(StringComparer.Ordinal).ToList(),
                user),
            null);
    }

    private static DebloatCommandParse Fail(string error) => new(null, error);

    private static bool TryValue(string[] args, ref int index, out string value)
    {
        if (index + 1 >= args.Length)
        {
            value = string.Empty;
            return false;
        }

        value = args[++index];
        return true;
    }

    private static IEnumerable<string> Split(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
