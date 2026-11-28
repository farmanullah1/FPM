namespace PCManager.Core.Debloat;

public enum DebloatPlanStatus
{
    Ready,
    SkippedOs,
    SkippedUnsupported,
    SkippedProtected,
    SkippedConflict,
}

public sealed class DebloatPlanItem
{
    public DebloatTweak Tweak { get; init; } = new();
    public DebloatPlanStatus Status { get; init; }
    public string Reason { get; init; } = string.Empty;

    public bool IsRunnable => Status == DebloatPlanStatus.Ready;
}

public sealed class DebloatPlan
{
    public List<DebloatPlanItem> Items { get; } = new();

    public IEnumerable<DebloatTweak> Runnable => Items.Where(item => item.IsRunnable).Select(item => item.Tweak);

    public bool RequiresReboot => Items.Any(item => item.IsRunnable && item.Tweak.RequiresReboot);

    public bool RequiresExplorerRestart => Items.Any(item => item.IsRunnable && item.Tweak.RequiresExplorerRestart);

    public bool HasHighRisk => Items.Any(item =>
        item.IsRunnable && string.Equals(item.Tweak.Risk, "High", StringComparison.Ordinal));
}

/// <summary>
/// Turns ids into an ordered, typed plan. Refusals happen here, before any elevation: unknown ids,
/// unsupported action types, protected packages and OS gates never reach the helper.
/// </summary>
public static class DebloatPlanner
{
    public static DebloatPlan Plan(
        DebloatCatalog catalog,
        IEnumerable<string> tweakIds,
        int osBuild,
        bool isWindows11)
    {
        var plan = new DebloatPlan();
        var categoryOrder = catalog.Categories
            .Select((category, index) => (category.Name, index))
            .ToDictionary(pair => pair.Name, pair => pair.index, StringComparer.OrdinalIgnoreCase);

        foreach (string requestedId in tweakIds.Distinct(StringComparer.Ordinal))
        {
            DebloatTweak? tweak = catalog.Tweaks.FirstOrDefault(candidate => candidate.Id == requestedId);
            if (tweak is null)
            {
                plan.Items.Add(new DebloatPlanItem
                {
                    Tweak = new DebloatTweak { Id = requestedId },
                    Status = DebloatPlanStatus.SkippedUnsupported,
                    Reason = "Unknown tweak id.",
                });
                continue;
            }

            plan.Items.Add(Evaluate(tweak, osBuild, isWindows11));
        }

        plan.Items.Sort((left, right) =>
        {
            int leftCategory = categoryOrder.GetValueOrDefault(left.Tweak.Category, int.MaxValue);
            int rightCategory = categoryOrder.GetValueOrDefault(right.Tweak.Category, int.MaxValue);
            return leftCategory != rightCategory
                ? leftCategory.CompareTo(rightCategory)
                : string.CompareOrdinal(left.Tweak.Id, right.Tweak.Id);
        });

        RefuseConflicts(plan);
        return plan;
    }

    private static DebloatPlanItem Evaluate(DebloatTweak tweak, int osBuild, bool isWindows11)
    {
        if (tweak.Actions.OfType<AppxRemoveAction>().Any(action =>
                DebloatCatalogValidator.IsProtectedPackage(action.PackageId)))
        {
            return Skip(tweak, DebloatPlanStatus.SkippedProtected,
                "The tweak targets a protected package.");
        }

        if (tweak.Actions.Any(action => action is RegistryKeyDeleteAction))
        {
            return Skip(tweak, DebloatPlanStatus.SkippedUnsupported,
                "Key deletion needs a typed key snapshot; not supported by this engine yet.");
        }

        if (tweak.Actions.Any(action => action is AppxRemoveAction))
        {
            return Skip(tweak, DebloatPlanStatus.SkippedUnsupported,
                "App package removal is handled by the Apps flow, not the registry executor.");
        }

        if (tweak.Os.Count > 0)
        {
            string key = isWindows11 ? "win11" : "win10";
            if (!tweak.Os.TryGetValue(key, out DebloatOsGate? gate))
            {
                return Skip(tweak, DebloatPlanStatus.SkippedOs,
                    $"Only applies to {(isWindows11 ? "Windows 10" : "Windows 11")}.");
            }

            if (gate.MinBuild is { } min && osBuild < min)
            {
                return Skip(tweak, DebloatPlanStatus.SkippedOs, $"Needs build {min} or newer.");
            }

            if (gate.MaxBuild is { } max && osBuild > max)
            {
                return Skip(tweak, DebloatPlanStatus.SkippedOs, $"Only applies up to build {max}.");
            }
        }

        return new DebloatPlanItem { Tweak = tweak, Status = DebloatPlanStatus.Ready };
    }

    private static void RefuseConflicts(DebloatPlan plan)
    {
        List<DebloatConflict> conflicts = DebloatConflictDetector
            .Find(plan.Items.Where(item => item.IsRunnable).Select(item => item.Tweak))
            .ToList();

        if (conflicts.Count == 0)
        {
            return;
        }

        var byTweak = conflicts
            .SelectMany(conflict => conflict.TweakIds.Select(id => (Id: id, Conflict: conflict)))
            .GroupBy(pair => pair.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        for (int index = 0; index < plan.Items.Count; index++)
        {
            DebloatPlanItem item = plan.Items[index];
            if (!byTweak.TryGetValue(item.Tweak.Id, out var found))
            {
                continue;
            }

            string targets = string.Join("; ", found.Select(pair =>
            {
                string others = string.Join(", ", pair.Conflict.TweakIds.Where(id => id != item.Tweak.Id));
                return $"{pair.Conflict.Target} (with {others})";
            }));

            plan.Items[index] = Skip(
                item.Tweak,
                DebloatPlanStatus.SkippedConflict,
                "Conflicts with another selected tweak: " + targets + ". Choose one alternative.");
        }
    }

    private static DebloatPlanItem Skip(DebloatTweak tweak, DebloatPlanStatus status, string reason) =>
        new() { Tweak = tweak, Status = status, Reason = reason };
}
