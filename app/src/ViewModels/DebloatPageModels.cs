using CommunityToolkit.Mvvm.ComponentModel;
using PCManager.Core.Debloat;

namespace PCManager.ViewModels;

/// <summary>One category row in the D3 left list, with its catalog count.</summary>
public sealed class DebloatCategoryRow(string name, int count, bool isAll = false) : ObservableObject
{
    public string Name { get; } = name;
    public int Count { get; } = count;
    public bool IsAll { get; } = isAll;
    public string CountText => $"{Count}";
}

/// <summary>One selectable catalog tweak; unsupported tweaks stay visible but are refused.</summary>
public sealed partial class DebloatTweakRow : ObservableObject
{
    public DebloatTweakRow(DebloatTweak tweak, bool selectable, string reason)
    {
        Tweak = tweak;
        IsSelectable = selectable;
        Reason = reason;
    }

    public DebloatTweak Tweak { get; }
    public bool IsSelectable { get; }
    public string Reason { get; }

    public string Title => Tweak.Title;
    public string Category => Tweak.Category;
    public string IconKey => Tweak.Category switch
    {
        "Privacy & Suggested Content" => "Privacy",
        "AI" => "Ai",
        "Start Menu & Search" => "StartMenu",
        "Taskbar" => "TaskbarTweaks",
        "File Explorer" => "FileExplorer",
        "Multi-tasking" => "Multitasking",
        "Optional Windows Features" => "OptionalFeatures",
        _ => "WindowsDebloat",
    };
    public string RiskText => $"{Tweak.Risk} risk";
    public string ImpactText => Tweak.Risk switch
    {
        "High" => "High risk · changes system behaviour; review before applying",
        "Medium" => "Medium risk · may change how Windows behaves",
        _ => "Low risk · reversible from the journal",
    };
    public string StateText => IsSelectable ? ImpactText : $"Not supported yet · {Reason}";
    public string AutomationName => $"{Title}, {RiskText}";

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isApplied;
}

/// <summary>One line of the computed plan (ready/skipped) shown in review.</summary>
public sealed class DebloatPlanRow(DebloatPlanItem item)
{
    public string Title { get; } = item.Tweak.Title;
    public string StatusText { get; } = item.IsRunnable ? "Ready" : $"Skipped · {item.Reason}";
    public string RiskText { get; } = item.IsRunnable ? $"{item.Tweak.Risk} risk" : string.Empty;
    public bool IsReady { get; } = item.IsRunnable;
}

/// <summary>Catalog app row. Selection is for preset files; removal is still refused.</summary>
public sealed partial class DebloatAppRow(DebloatApp app) : ObservableObject
{
    public DebloatApp App { get; } = app;
    public string Title { get; } = app.Title;
    public string Recommendation { get; } = app.Recommendation;
    public string PackagesText { get; } = string.Join(", ", app.PackageIds);
    public string RemovalText { get; } = app.RemovalMethod == "Appx"
        ? "Package removal waits for a journaled Apps flow"
        : app.RemovalMethod;

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>One typed user target shown in the D4 profile picker.</summary>
public sealed record DebloatTargetOption(string Token, string DisplayName, string Warning);

public sealed record DebloatDriftItem(string TweakId, string Title, bool StillApplied);

/// <summary>Report-only drift summary; never reapplies anything.</summary>
public static class DebloatDrift
{
    public static string Summarize(IReadOnlyList<DebloatDriftItem> items)
    {
        int drifted = items.Count(item => !item.StillApplied);
        int applied = items.Count - drifted;
        string names = string.Join(", ", items.Where(item => !item.StillApplied).Select(item => item.Title));

        return drifted == 0
            ? $"Drift check: all {applied} journaled tweak(s) are still applied."
            : $"Drift check: {applied} still applied, {drifted} reverted or changed: {names}. " +
              "Nothing was reapplied.";
    }
}
