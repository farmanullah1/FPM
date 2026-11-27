using Xunit;
using PCManager.Core.Debloat;

namespace PCManager.Tests;

/// <summary>D1: the ported catalog loads, validates and respects the safety contract.</summary>
public sealed class DebloatCatalogTests
{
    private static readonly DebloatCatalog Catalog = DebloatCatalogLoader.LoadEmbedded();

    [Fact]
    public void EmbeddedCatalogIsValid()
    {
        Assert.Empty(DebloatCatalogValidator.Validate(Catalog));
        Assert.Equal("1.0", Catalog.Version);
        Assert.Contains("Win11Debloat", Catalog.Source);
    }

    [Fact]
    public void PortedSurfaceMatchesTheReference()
    {
        Assert.Equal(90, Catalog.Tweaks.Count);
        Assert.Equal(161, Catalog.Apps.Count);
        Assert.Equal(11, Catalog.ExcludedFeatures.Count);
        Assert.Equal(2, Catalog.AppPresets.Count);
    }

    [Fact]
    public void EveryTweakHasReverseAndDetectionData()
    {
        foreach (DebloatTweak tweak in Catalog.Tweaks)
        {
            Assert.NotEmpty(tweak.Actions);
            Assert.NotEmpty(tweak.Detect);
            Assert.True(tweak.Revert.Count > 0 || tweak.RevertNote.Length > 0, tweak.Id);
            Assert.StartsWith("win11debloat.", tweak.Id);
            Assert.NotEmpty(tweak.ReferenceFile);
        }
    }

    [Fact]
    public void NoRegistryActionEscapesTheAllowList()
    {
        foreach (DebloatTweak tweak in Catalog.Tweaks)
        {
            foreach (DebloatAction action in tweak.Actions.Concat(tweak.Revert))
            {
                if (action is OptionalFeatureAction or AppxRemoveAction)
                {
                    continue;
                }

                Assert.Contains(action.Hive, DebloatCatalogValidator.AllowedHives);
                Assert.False(action.Hive == "HKU" &&
                             !action.Path.StartsWith("Default", StringComparison.OrdinalIgnoreCase));
                Assert.Contains(
                    action.Path.Split('\\')[0],
                    DebloatCatalogValidator.AllowedPathRoots,
                    StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void ProtectedAppsAreNeverOffered()
    {
        var offered = Catalog.Apps.SelectMany(app => app.PackageIds.Prepend(app.Id)).ToList();

        foreach (string protectedId in DebloatCatalogValidator.ProtectedPackages)
        {
            Assert.DoesNotContain(offered, id =>
                id.Equals(protectedId, StringComparison.OrdinalIgnoreCase) ||
                id.StartsWith(protectedId + ".", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void OsGatingUsesRealBuildNumbers()
    {
        var gated = Catalog.Tweaks.Where(tweak => tweak.Os.Count > 0).ToList();

        Assert.NotEmpty(gated);
        Assert.All(gated, tweak => Assert.All(tweak.Os.Values, gate =>
        {
            Assert.True(gate.MinBuild is null or >= 10240);
            Assert.True(gate.MaxBuild is null or >= 10240);
        }));
    }

    [Fact]
    public void ValidatorRejectsAPathOutsideTheAllowList()
    {
        DebloatCatalog tampered = MinimalCatalog(new RegistrySetAction
        {
            Hive = "HKCU",
            Path = "Run",
            RegistryName = "Evil",
            ValueType = "String",
            Value = System.Text.Json.JsonDocument.Parse("\"x\"").RootElement,
        });

        Assert.Contains(DebloatCatalogValidator.Validate(tampered), error => error.Contains("allow-list"));
    }

    [Fact]
    public void ValidatorRejectsProtectedAppRemoval()
    {
        DebloatCatalog tampered = MinimalCatalog(new AppxRemoveAction
        {
            PackageId = "Microsoft.WindowsStore",
        });

        Assert.Contains(DebloatCatalogValidator.Validate(tampered), error => error.Contains("protected"));
    }

    private static DebloatCatalog MinimalCatalog(DebloatAction action) => new()
    {
        Version = "1.0",
        Tweaks = new List<DebloatTweak>
        {
            new()
            {
                Id = "win11debloat.Test",
                Category = "System",
                Title = "Test",
                Risk = "Low",
                AppliesTo = "CurrentUser",
                Actions = new List<DebloatAction> { action },
                Revert = new List<DebloatAction>(),
                RevertNote = "journal",
                Detect = new List<DebloatProbe>
                {
                    new RegistryKeyAbsentProbe { Hive = "HKCU", Path = "Software\\Test" },
                },
                ReferenceFile = "Regfiles/Test.reg",
            },
        },
    };
}
