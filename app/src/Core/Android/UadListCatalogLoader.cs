using System.Reflection;

namespace PCManager.Core.Android;

public static class UadListCatalogLoader
{
    public const string ResourceName = "PCManager.Core.Assets.android.uad_lists.json";

    public static UadListCatalog LoadEmbedded()
    {
        Assembly assembly = typeof(UadListCatalogLoader).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded UAD list '{ResourceName}' is missing.");
        using StreamReader reader = new(stream);
        string json = reader.ReadToEnd();

        if (!UadListParser.TryParse(json, UadListOrigin.Embedded, out UadListCatalog? catalog, out var errors))
        {
            throw new InvalidOperationException(
                "The embedded UAD list is invalid: " + string.Join("; ", errors));
        }

        return catalog!;
    }
}
