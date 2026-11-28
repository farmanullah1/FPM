using System.Text.Json;

namespace PCManager.Core.Debloat;

/// <summary>Two selected tweaks ask for different final states on the same typed target.</summary>
public sealed record DebloatConflict(string Target, IReadOnlyList<string> TweakIds);

/// <summary>
/// Finds contradictory selections before elevation: same registry value with different desired
/// data, or the same optional feature both enabled and disabled. A repeated identical target is
/// not a conflict.
/// </summary>
public static class DebloatConflictDetector
{
    public static IReadOnlyList<DebloatConflict> Find(IEnumerable<DebloatTweak> tweaks)
    {
        var groups = new Dictionary<string, Group>(StringComparer.OrdinalIgnoreCase);

        foreach (DebloatTweak tweak in tweaks)
        {
            foreach (DebloatAction action in tweak.Actions)
            {
                switch (action)
                {
                    case RegistrySetAction set:
                        Add(
                            groups,
                            $"{set.Hive}\\{set.Path}\\{set.RegistryName}",
                            $"{set.ValueType}:{set.Value.GetRawText()}",
                            tweak.Id);
                        break;
                    case RegistryDeleteAction delete:
                        Add(
                            groups,
                            $"{delete.Hive}\\{delete.Path}\\{delete.RegistryName}",
                            "absent",
                            tweak.Id);
                        break;
                    case OptionalFeatureAction feature:
                        Add(
                            groups,
                            $"feature:{feature.FeatureName}",
                            feature.Enable ? "enabled" : "disabled",
                            tweak.Id);
                        break;
                }
            }
        }

        return groups
            .Where(pair => pair.Value.TweakIds.Count > 1 && pair.Value.Signatures.Count > 1)
            .Select(pair => new DebloatConflict(pair.Key, pair.Value.TweakIds.ToList()))
            .OrderBy(conflict => conflict.Target, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void Add(
        Dictionary<string, Group> groups,
        string target,
        string signature,
        string tweakId)
    {
        if (!groups.TryGetValue(target, out Group? group))
        {
            group = new Group();
            groups[target] = group;
        }

        group.Signatures.Add(signature);
        if (!group.TweakIds.Contains(tweakId, StringComparer.Ordinal))
        {
            group.TweakIds.Add(tweakId);
        }
    }

    private sealed class Group
    {
        public HashSet<string> Signatures { get; } = new(StringComparer.Ordinal);
        public List<string> TweakIds { get; } = new();
    }
}
