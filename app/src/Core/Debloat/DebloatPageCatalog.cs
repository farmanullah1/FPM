using System.Text;
using System.Text.Json;

namespace PCManager.Core.Debloat;

/// <summary>Filtering, review text and run-report presentation shared by the D3 page and its tests.</summary>
public static class DebloatPageCatalog
{
    public static IReadOnlyList<DebloatTweak> Filter(
        DebloatCatalog catalog,
        string? categoryName,
        string? search)
    {
        IEnumerable<DebloatTweak> tweaks = catalog.Tweaks;

        if (!string.IsNullOrWhiteSpace(categoryName))
        {
            tweaks = tweaks.Where(tweak =>
                string.Equals(tweak.Category, categoryName, StringComparison.OrdinalIgnoreCase));
        }

        string query = (search ?? string.Empty).Trim();
        if (query.Length > 0)
        {
            tweaks = tweaks.Where(tweak =>
                tweak.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                tweak.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                tweak.Id.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        return tweaks
            .OrderBy(tweak => tweak.Category)
            .ThenBy(tweak => tweak.Title)
            .ToList();
    }

    /// <summary>The typed "what will change" review; current values are captured at apply time.</summary>
    public static string DescribeActions(DebloatTweak tweak)
    {
        var builder = new StringBuilder();

        foreach (DebloatAction action in tweak.Actions.Take(6))
        {
            builder.AppendLine(action switch
            {
                RegistrySetAction set =>
                    $"{set.Hive}\\{set.Path} · {set.RegistryName} → {Format(set.ValueType, set.Value)}",
                RegistryDeleteAction delete =>
                    $"{delete.Hive}\\{delete.Path} · {delete.RegistryName} → removed",
                OptionalFeatureAction feature =>
                    $"Optional feature {feature.FeatureName} → {(feature.Enable ? "enabled" : "disabled")}",
                AppxRemoveAction appx => $"Package {appx.PackageId} → removal is not wired",
                _ => "Key deletion is not wired",
            });
        }

        if (tweak.Actions.Count > 6)
        {
            builder.AppendLine($"… and {tweak.Actions.Count - 6} more change(s)");
        }

        return builder.ToString().TrimEnd();
    }

    public static string BuildImpactSummary(DebloatPlan plan)
    {
        if (plan.Items.Count == 0)
        {
            return "No tweaks selected.";
        }

        int ready = plan.Items.Count(item => item.IsRunnable);
        int skipped = plan.Items.Count - ready;
        int low = plan.Items.Count(item => item.IsRunnable && item.Tweak.Risk == "Low");
        int medium = plan.Items.Count(item => item.IsRunnable && item.Tweak.Risk == "Medium");
        int high = plan.Items.Count(item => item.IsRunnable && item.Tweak.Risk == "High");

        string risk = string.Join(", ", new[]
        {
            low > 0 ? $"{low} Low" : null,
            medium > 0 ? $"{medium} Medium" : null,
            high > 0 ? $"{high} High" : null,
        }.Where(part => part is not null));

        return $"{plan.Items.Count} selected · {ready} ready · {skipped} skipped · risk {risk}" +
               (plan.RequiresReboot ? " · reboot required" : string.Empty) +
               (plan.RequiresExplorerRestart ? " · Explorer restart" : string.Empty);
    }

    private static string Format(string valueType, JsonElement value) => valueType switch
    {
        "DWord" or "QWord" => value.ToString(),
        "MultiString" => string.Join("; ", value.EnumerateArray().Select(item => item.GetString())),
        _ => value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString(),
    };
}

/// <summary>The report card after an apply, including the partially rolled-back state.</summary>
public sealed class DebloatRunReport
{
    public bool IsFailure { get; init; }
    public bool RolledBack { get; init; }
    public bool RequiresReboot { get; init; }
    public bool CanUndo { get; init; }
    public string Headline { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;

    public static DebloatRunReport From(DebloatRunRecord record, int selected)
    {
        int applied = record.Entries.Count;
        int skipped = Math.Max(0, selected - applied);

        if (record.RolledBack)
        {
            return new DebloatRunReport
            {
                IsFailure = true,
                RolledBack = true,
                RequiresReboot = record.RequiresReboot,
                Headline = "The run was partially rolled back",
                Detail = $"{record.FailureMessage} {applied} tweak(s) had been applied; " +
                         "the engine restored them in reverse order and the journal records the rollback. " +
                         "Nothing from this run is left applied.",
            };
        }

        if (!record.Completed)
        {
            return new DebloatRunReport
            {
                IsFailure = true,
                RequiresReboot = record.RequiresReboot,
                Headline = "The run stopped",
                Detail = record.FailureMessage,
            };
        }

        if (applied == 0)
        {
            return new DebloatRunReport
            {
                Headline = "Nothing to apply",
                Detail = record.FailureMessage,
            };
        }

        return new DebloatRunReport
        {
            RequiresReboot = record.RequiresReboot,
            CanUndo = true,
            Headline = $"Applied {applied} tweak(s)",
            Detail = $"{skipped} selected tweak(s) were already applied or skipped. " +
                     $"Restore point: {record.RestorePointStatus}. Backup: {record.BackupPath}." +
                     (record.RequiresReboot ? " Restart Windows to finish." : string.Empty),
        };
    }
}
