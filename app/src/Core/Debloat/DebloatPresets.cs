namespace PCManager.Core.Debloat;

/// <summary>One named selection rule for the Windows tab (D3).</summary>
public sealed record DebloatPreset(string Id, string Name, string Description);

/// <summary>
/// The reference catalog has no tweak presets, so D3's presets are documented rules over the
/// catalog's own category and risk data. "Custom" keeps the user's current selection.
/// </summary>
public static class DebloatPresetCatalog
{
    public const string CustomId = "custom";

    public static IReadOnlyList<DebloatPreset> All { get; } = new[]
    {
        new DebloatPreset("recommended", "Recommended safe",
            "Every tweak rated Low risk."),
        new DebloatPreset("privacy", "Privacy focus",
            "Privacy, AI and search suggestions, excluding High risk."),
        new DebloatPreset("performance", "Performance",
            "System, multi-tasking and gaming tweaks, excluding High risk."),
        new DebloatPreset("minimal", "Minimal (reference defaults)",
            "Only the Low-risk appearance and Start menu tweaks."),
        new DebloatPreset(CustomId, "Custom", "Your own selection."),
    };

    public static IReadOnlyList<string> Select(DebloatCatalog catalog, string presetId)
    {
        IEnumerable<DebloatTweak> tweaks = catalog.Tweaks;

        return presetId switch
        {
            "recommended" => tweaks.Where(tweak => tweak.Risk == "Low").Select(tweak => tweak.Id).ToList(),
            "privacy" => tweaks.Where(tweak =>
                    tweak.Risk != "High" && IsPrivacy(tweak))
                .Select(tweak => tweak.Id)
                .ToList(),
            "performance" => tweaks.Where(tweak =>
                    tweak.Risk != "High" && IsPerformance(tweak))
                .Select(tweak => tweak.Id)
                .ToList(),
            "minimal" => tweaks.Where(tweak =>
                    tweak.Risk == "Low" &&
                    tweak.Category is "Appearance" or "Start Menu & Search")
                .Select(tweak => tweak.Id)
                .ToList(),
            _ => Array.Empty<string>(),
        };
    }

    public static bool IsPrivacy(DebloatTweak tweak) =>
        tweak.Category is "Privacy & Suggested Content" or "AI" or "Start Menu & Search";

    public static bool IsPerformance(DebloatTweak tweak) =>
        tweak.Category is "System" or "Multi-tasking" or "Gaming";
}
