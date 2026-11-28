using System.Text.Json;
using PCManager.Core.Debloat;
using Xunit;

namespace PCManager.Tests;

public sealed class DebloatConflictTests
{
    private static readonly DebloatCatalog Catalog = DebloatCatalogLoader.LoadEmbedded();

    [Fact]
    public void Opposite_values_on_one_target_are_refused_before_elevation()
    {
        DebloatCatalog catalog = CatalogWith(
            ("a", "HKCU", @"Software\FPM", "Mode", 1),
            ("b", "HKCU", @"Software\FPM", "Mode", 2));

        DebloatPlan plan = DebloatPlanner.Plan(catalog, new[] { "a", "b" }, 22631, true);

        Assert.All(plan.Items, item => Assert.Equal(DebloatPlanStatus.SkippedConflict, item.Status));
        Assert.Empty(plan.Runnable);
        Assert.All(plan.Items, item => Assert.Contains("Conflicts with", item.Reason));
    }

    [Fact]
    public void Identical_values_on_one_target_are_not_a_conflict()
    {
        DebloatCatalog catalog = CatalogWith(
            ("a", "HKCU", @"Software\FPM", "Mode", 1),
            ("b", "HKCU", @"Software\FPM", "Mode", 1));

        DebloatPlan plan = DebloatPlanner.Plan(catalog, new[] { "a", "b" }, 22631, true);

        Assert.All(plan.Items, item => Assert.True(item.IsRunnable));
    }

    [Fact]
    public void Real_spotlight_pair_is_refused_as_a_conflict()
    {
        DebloatPlan plan = DebloatPlanner.Plan(
            Catalog,
            new[] { "win11debloat.EnableDesktopSpotlight", "win11debloat.DisableDesktopSpotlight" },
            22631,
            true);

        Assert.All(plan.Items, item => Assert.Equal(DebloatPlanStatus.SkippedConflict, item.Status));
    }

    private static DebloatCatalog CatalogWith(params (string Id, string Hive, string Path, string Name, int Value)[] tweaks)
    {
        var catalog = new DebloatCatalog
        {
            Categories = { new DebloatCategory { Id = "test", Name = "Test" } },
        };

        foreach (var (id, hive, path, name, value) in tweaks)
        {
            catalog.Tweaks.Add(new DebloatTweak
            {
                Id = id,
                Category = "Test",
                Title = id,
                Risk = "Low",
                Actions =
                {
                    new RegistrySetAction
                    {
                        Hive = hive,
                        Path = path,
                        RegistryName = name,
                        ValueType = "DWord",
                        Value = JsonSerializer.SerializeToElement(value),
                    },
                },
            });
        }

        return catalog;
    }
}

public sealed class DebloatRestoreGuardTests
{
    private static readonly DebloatTweak Tweak = DebloatCatalogLoader.LoadEmbedded().Tweaks
        .First(candidate => candidate.Actions.Any(action => action is RegistrySetAction));

    [Fact]
    public void Own_target_is_accepted_but_foreign_path_is_rejected()
    {
        RegistrySetAction action = Tweak.Actions.OfType<RegistrySetAction>().First();
        RegistryValueState own = State(action.Hive, action.Path, action.RegistryName);
        RegistryValueState foreign = State(action.Hive, action.Path, action.RegistryName + ".other");

        Assert.Null(DebloatRestoreGuard.Validate(Tweak, new[] { own }));
        Assert.Contains("not a target", DebloatRestoreGuard.Validate(Tweak, new[] { foreign }));
    }

    [Fact]
    public void Hive_outside_the_allow_list_is_rejected()
    {
        var tweak = new DebloatTweak
        {
            Id = "test.hkcr",
            Actions =
            {
                new RegistrySetAction
                {
                    Hive = "HKCR",
                    Path = "Test",
                    RegistryName = "Value",
                    ValueType = "String",
                    Value = JsonSerializer.SerializeToElement("x"),
                },
            },
        };

        Assert.Contains(
            "outside the allow-list",
            DebloatRestoreGuard.Validate(tweak, new[] { State("HKCR", "Test", "Value") }));
    }

    private static RegistryValueState State(string hive, string path, string name) => new()
    {
        Hive = hive,
        Path = path,
        Name = name,
        Existed = true,
        ValueType = "String",
        Value = JsonSerializer.SerializeToElement("x"),
    };
}

public sealed class DebloatCatalogDetectionTests
{
    private static readonly DebloatCatalog Catalog = DebloatCatalogLoader.LoadEmbedded();

    [Fact]
    public void Every_detection_probe_matches_the_action_that_satisfies_it()
    {
        foreach (DebloatTweak tweak in Catalog.Tweaks)
        {
            var sets = tweak.Actions.OfType<RegistrySetAction>()
                .ToDictionary(Target, StringComparer.OrdinalIgnoreCase);
            var deletes = tweak.Actions.OfType<RegistryDeleteAction>()
                .Select(Target)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var keyDeletes = tweak.Actions.OfType<RegistryKeyDeleteAction>()
                .Select(Target)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var features = tweak.Actions.OfType<OptionalFeatureAction>()
                .ToDictionary(action => action.FeatureName, action => action.Enable, StringComparer.OrdinalIgnoreCase);
            var apps = tweak.Actions.OfType<AppxRemoveAction>()
                .Select(action => action.PackageId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (RegistryEqualsProbe probe in tweak.Detect.OfType<RegistryEqualsProbe>())
            {
                Assert.True(sets.TryGetValue(Target(probe), out RegistrySetAction? action),
                    $"{tweak.Id}: no RegistrySet for {Target(probe)}");
                Assert.Equal(probe.ValueType, action!.ValueType);
                Assert.Equal(probe.Value.GetRawText(), action.Value.GetRawText());
            }

            foreach (RegistryAbsentProbe probe in tweak.Detect.OfType<RegistryAbsentProbe>())
            {
                Assert.Contains(Target(probe), deletes);
            }

            foreach (RegistryKeyAbsentProbe probe in tweak.Detect.OfType<RegistryKeyAbsentProbe>())
            {
                Assert.Contains(Target(probe), keyDeletes);
            }

            foreach (OptionalFeatureProbe probe in tweak.Detect.OfType<OptionalFeatureProbe>())
            {
                Assert.True(features.TryGetValue(probe.FeatureName, out bool enabled),
                    $"{tweak.Id}: no OptionalFeature action for {probe.FeatureName}");
                Assert.Equal(probe.ExpectEnabled, enabled);
            }

            foreach (AppxAbsentProbe probe in tweak.Detect.OfType<AppxAbsentProbe>())
            {
                Assert.Contains(probe.PackageId, apps);
            }

            Assert.All(sets.Values, action => Assert.Contains(
                tweak.Detect.OfType<RegistryEqualsProbe>(),
                probe => Target(probe) == Target(action) &&
                         probe.Value.GetRawText() == action.Value.GetRawText()));
            Assert.All(tweak.Actions.OfType<RegistryDeleteAction>(), action =>
                Assert.Contains(tweak.Detect.OfType<RegistryAbsentProbe>(), probe => Target(probe) == Target(action)));
            Assert.All(tweak.Actions.OfType<RegistryKeyDeleteAction>(), action =>
                Assert.Contains(tweak.Detect.OfType<RegistryKeyAbsentProbe>(), probe => Target(probe) == Target(action)));
            Assert.All(tweak.Actions.OfType<OptionalFeatureAction>(), action =>
                Assert.Contains(tweak.Detect.OfType<OptionalFeatureProbe>(), probe =>
                    probe.FeatureName == action.FeatureName && probe.ExpectEnabled == action.Enable));
            Assert.All(tweak.Actions.OfType<AppxRemoveAction>(), action =>
                Assert.Contains(tweak.Detect.OfType<AppxAbsentProbe>(), probe => probe.PackageId == action.PackageId));
        }
    }

    private static string Target(DebloatAction action) =>
        action is OptionalFeatureAction feature
            ? $"feature:{feature.FeatureName}"
            : action is AppxRemoveAction appx
                ? $"appx:{appx.PackageId}"
                : $"{action.Hive}\\{action.Path}\\{(action as RegistrySetAction)?.RegistryName ?? (action as RegistryDeleteAction)?.RegistryName}";

    private static string Target(DebloatProbe probe) =>
        probe is OptionalFeatureProbe feature
            ? $"feature:{feature.FeatureName}"
            : probe is AppxAbsentProbe appx
                ? $"appx:{appx.PackageId}"
                : $"{probe.Hive}\\{probe.Path}\\{(probe as RegistryEqualsProbe)?.RegistryName ?? (probe as RegistryAbsentProbe)?.RegistryName}";
}

public sealed class DebloatReferenceParityTests
{
    private static readonly DebloatCatalog Catalog = DebloatCatalogLoader.LoadEmbedded();

    [Fact]
    public void Feature_map_covers_every_reference_feature_as_ported_or_excluded()
    {
        string path = Path.Combine(RepoRoot(), "docs", "dev", "win11debloat-feature-map.md");
        Assert.True(File.Exists(path), path);

        List<string[]> rows = File.ReadAllLines(path)
            .Where(line => line.StartsWith("| `", StringComparison.Ordinal))
            .Select(line => line.Trim('|').Split('|').Select(cell => cell.Trim().Trim('`')).ToArray())
            .ToList();

        Assert.Equal(106, rows.Count);
        Assert.Equal(106, rows.Select(row => row[0]).Distinct(StringComparer.Ordinal).Count());

        List<string[]> ported = rows.Where(row => row[2] == "ported").ToList();
        List<string[]> excluded = rows.Where(row => row[2] == "intentionally-excluded").ToList();

        Assert.Equal(90, ported.Count);
        Assert.Equal(16, excluded.Count);

        var catalogIds = Catalog.Tweaks.Select(tweak => tweak.Id).ToHashSet(StringComparer.Ordinal);
        var portedIds = ported.Select(row => row[1]).ToHashSet(StringComparer.Ordinal);
        var catalogExcluded = Catalog.ExcludedFeatures
            .Select(feature => feature.FeatureId)
            .ToHashSet(StringComparer.Ordinal);
        var mapExcluded = excluded.Select(row => row[0]).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(catalogIds, portedIds);
        Assert.Equal(catalogExcluded, mapExcluded);
        Assert.All(ported, row => Assert.StartsWith("win11debloat.", row[1]));
        Assert.All(excluded, row =>
        {
            Assert.Equal("—", row[1]);
            Assert.False(string.IsNullOrWhiteSpace(row[3]), row[0]);
        });
    }

    private static string RepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "app", "PCManager.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not find the repository root.");
    }
}
