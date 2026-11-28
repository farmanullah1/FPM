using PCManager.Core.Android;
using Xunit;

namespace PCManager.Tests;

public sealed class UadListParserTests
{
    private const string TwoPackages = """
        [
          {
            "id": "com.example.one",
            "list": "Oem",
            "description": "First package",
            "dependencies": [],
            "neededBy": ["com.example.two"],
            "labels": ["system"],
            "removal": "Recommended"
          },
          {
            "id": "android",
            "list": "Aosp",
            "description": "Core package",
            "dependencies": [],
            "neededBy": [],
            "labels": [],
            "removal": "Unsafe"
          }
        ]
        """;

    [Fact]
    public void Valid_list_parses_with_typed_values_and_a_hash()
    {
        Assert.True(UadListParser.TryParse(
            TwoPackages, UadListOrigin.Embedded, out UadListCatalog? catalog, out var errors));

        Assert.Empty(errors);
        Assert.NotNull(catalog);
        Assert.Equal(2, catalog!.Packages.Count);
        UadPackageEntry first = catalog.ById["com.example.one"];
        Assert.Equal(UadListKind.Oem, first.List);
        Assert.Equal(UadRemovalTier.Recommended, first.Removal);
        Assert.Equal(new[] { "com.example.two" }, first.NeededBy);
        Assert.Equal(64, catalog.Sha256.Length);
        Assert.Equal(catalog.Sha256.ToLowerInvariant(), catalog.Sha256);
        Assert.Equal(UadListOrigin.Embedded, catalog.Origin);
    }

    [Theory]
    [InlineData("Oem", "Recommended", "1com.example")]
    [InlineData("Unknown", "Recommended", "com.example.one")]
    [InlineData("Oem", "Unknown", "com.example.one")]
    [InlineData("Oem", "Recommended", "bad id")]
    public void Invalid_schema_values_are_rejected(string list, string removal, string id)
    {
        string json = $$"""
            [{"id":"{{id}}","list":"{{list}}","description":"x","removal":"{{removal}}"}]
            """;

        Assert.False(UadListParser.TryParse(
            json, UadListOrigin.Embedded, out UadListCatalog? catalog, out var errors));

        Assert.Null(catalog);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Malformed_arrays_and_non_array_root_are_rejected()
    {
        const string badArray = """
            [{"id":"com.example.one","list":"Oem","description":"x","removal":"Recommended","dependencies":"no"}]
            """;
        const string badRoot = "{}";

        Assert.False(UadListParser.TryParse(badArray, UadListOrigin.Embedded, out _, out var arrayErrors));
        Assert.Contains("non-array", arrayErrors[0]);
        Assert.False(UadListParser.TryParse(badRoot, UadListOrigin.Embedded, out _, out var rootErrors));
        Assert.Contains("root", rootErrors[0]);
    }

    [Fact]
    public void Duplicate_ids_are_last_wins_and_reported()
    {
        const string duplicates = """
            [
              {"id":"com.example.one","list":"Oem","description":"old","removal":"Recommended"},
              {"id":"com.example.one","list":"Google","description":"new","removal":"Advanced"}
            ]
            """;

        Assert.True(UadListParser.TryParse(
            duplicates, UadListOrigin.Embedded, out UadListCatalog? catalog, out _));

        Assert.Single(catalog!.Packages);
        Assert.Equal("new", catalog.Packages[0].Description);
        Assert.Equal(UadListKind.Google, catalog.Packages[0].List);
        Assert.Equal(1, catalog.DuplicateIdCount);
        Assert.Equal(2, catalog.SourceRowCount);
    }

    [Fact]
    public void Embedded_reference_list_is_valid_and_matches_the_reference_shape()
    {
        UadListCatalog catalog = UadListCatalogLoader.LoadEmbedded();

        Assert.Equal(2394, catalog.Packages.Count);
        Assert.Equal(2400, catalog.SourceRowCount);
        Assert.Equal(6, catalog.DuplicateIdCount);
        Assert.Equal(140, catalog.Count(UadListKind.Aosp));
        Assert.Equal(129, catalog.Count(UadListKind.Carrier));
        Assert.Equal(165, catalog.Count(UadListKind.Google));
        Assert.Equal(273, catalog.Count(UadListKind.Misc));
        Assert.Equal(1445, catalog.Count(UadListKind.Oem));
        Assert.Equal(242, catalog.Count(UadListKind.Pending));
        Assert.True(catalog.ById.ContainsKey("android"));
        Assert.Equal(1339, catalog.Count(UadRemovalTier.Recommended));
        Assert.Equal(595, catalog.Count(UadRemovalTier.Expert));
    }
}
