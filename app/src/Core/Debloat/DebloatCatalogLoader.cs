using System.Reflection;
using System.Text.Json;

namespace PCManager.Core.Debloat;

/// <summary>
/// Loads the embedded, versioned catalog. Validation is part of loading: a catalog that breaks the
/// safety contract (hive or path allow-list, missing detect/revert, protected app) never reaches
/// the planner or the helper.
/// </summary>
public static class DebloatCatalogLoader
{
    public const string ResourceName = "PCManager.Core.Assets.debloat.catalog.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static DebloatCatalog LoadEmbedded()
    {
        Assembly assembly = typeof(DebloatCatalogLoader).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded catalog '{ResourceName}' is missing.");

        DebloatCatalog catalog = JsonSerializer.Deserialize<DebloatCatalog>(stream, Options)
            ?? throw new InvalidOperationException("The embedded debloat catalog is empty.");

        IReadOnlyList<string> errors = DebloatCatalogValidator.Validate(catalog);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "The embedded debloat catalog is invalid: " + string.Join("; ", errors));
        }

        return catalog;
    }
}
