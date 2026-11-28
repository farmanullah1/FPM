using System.Text.Json;
using System.Text.Json.Serialization;

namespace PCManager.Core.Debloat;

/// <summary>The on-disk fpm-debloat-preset.json shape: tweak ids and app ids only.</summary>
public sealed class DebloatPresetDocument
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("tweaks")]
    public List<string> Tweaks { get; set; } = new();

    [JsonPropertyName("apps")]
    public List<string> Apps { get; set; } = new();
}

public sealed record DebloatPresetImport(
    IReadOnlyList<string> TweakIds,
    IReadOnlyList<string> AppIds,
    IReadOnlyList<string> Warnings)
{
    public bool HasWarnings => Warnings.Count > 0;
}

public static class DebloatPresetFile
{
    public const int CurrentVersion = 1;
    public const string FileName = "fpm-debloat-preset.json";
    private const int MaxIds = 500;

    public static string Serialize(
        IEnumerable<string> tweakIds,
        IEnumerable<string> appIds)
    {
        var document = new DebloatPresetDocument
        {
            Version = CurrentVersion,
            Tweaks = tweakIds.Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToList(),
            Apps = appIds.Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToList(),
        };

        return JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Validates the document shape and drops unknown ids with a warning; never throws for one
    /// unknown id, because presets can be shared between catalog versions.
    /// </summary>
    public static DebloatPresetImport Parse(string json, DebloatCatalog catalog)
    {
        DebloatPresetDocument document;

        try
        {
            document = JsonSerializer.Deserialize<DebloatPresetDocument>(json)
                ?? throw new InvalidDataException("The preset file is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The preset file is not valid JSON: " + ex.Message, ex);
        }

        document.Tweaks ??= new List<string>();
        document.Apps ??= new List<string>();

        if (document.Version != CurrentVersion)
        {
            throw new InvalidDataException(
                $"Unsupported preset version {document.Version}; this build reads version {CurrentVersion}.");
        }

        if (document.Tweaks.Count > MaxIds || document.Apps.Count > MaxIds)
        {
            throw new InvalidDataException($"A preset may contain at most {MaxIds} tweak ids and app ids.");
        }

        var tweakIds = catalog.Tweaks.Select(tweak => tweak.Id).ToHashSet(StringComparer.Ordinal);
        var appIds = catalog.Apps.Select(app => app.Id).ToHashSet(StringComparer.Ordinal);
        var warnings = new List<string>();
        var selectedTweaks = new List<string>();
        var selectedApps = new List<string>();

        foreach (string id in document.Tweaks.Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            if (tweakIds.Contains(id))
            {
                selectedTweaks.Add(id);
            }
            else
            {
                warnings.Add($"Unknown tweak id '{id}' was ignored.");
            }
        }

        foreach (string id in document.Apps.Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            if (appIds.Contains(id))
            {
                selectedApps.Add(id);
            }
            else
            {
                warnings.Add($"Unknown app id '{id}' was ignored.");
            }
        }

        return new DebloatPresetImport(
            selectedTweaks.Distinct(StringComparer.Ordinal).ToList(),
            selectedApps.Distinct(StringComparer.Ordinal).ToList(),
            warnings);
    }
}
