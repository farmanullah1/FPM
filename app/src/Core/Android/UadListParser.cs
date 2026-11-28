using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PCManager.Core.Android;

/// <summary>
/// Validates the upstream UAD array before it can reach the package engine. Unknown fields are
/// ignored for forward compatibility; wrong types, unknown enum values and malformed arrays are
/// rejected. Duplicate ids (present upstream) are normalized last-wins, matching UAD's HashMap.
/// </summary>
public static class UadListParser
{
    public const int MaxJsonCharacters = 4_000_000;

    private static readonly Regex IdPattern = new(
        @"^[A-Za-z][A-Za-z0-9_]*(\.[A-Za-z0-9_]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool TryParse(
        string json,
        UadListOrigin origin,
        out UadListCatalog? catalog,
        out IReadOnlyList<string> errors)
    {
        var problems = new List<string>();
        catalog = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            errors = new[] { "The list is empty." };
            return false;
        }

        if (json.Length > MaxJsonCharacters)
        {
            errors = new[] { $"The list is larger than {MaxJsonCharacters:N0} characters." };
            return false;
        }

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 16,
            });
        }
        catch (JsonException ex)
        {
            errors = new[] { "Invalid JSON: " + ex.Message };
            return false;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                errors = new[] { "The root must be a JSON array." };
                return false;
            }

            var entries = new Dictionary<string, UadPackageEntry>(StringComparer.Ordinal);
            int sourceRows = 0;
            int duplicates = 0;

            foreach (JsonElement element in document.RootElement.EnumerateArray())
            {
                sourceRows++;

                if (element.ValueKind != JsonValueKind.Object)
                {
                    Add(problems, $"Row {sourceRows} is not an object.");
                    continue;
                }

                if (!TryReadString(element, "id", required: true, out string id, problems, sourceRows) ||
                    !IdPattern.IsMatch(id))
                {
                    Add(problems, $"Row {sourceRows} has an invalid package id.");
                    continue;
                }

                if (!TryReadEnum(element, "list", required: true, out string listText, problems, sourceRows) ||
                    !TryParseList(listText, out UadListKind list))
                {
                    Add(problems, $"Row {sourceRows} ({id}) has an unknown list.");
                    continue;
                }

                if (!TryReadString(element, "description", required: true, out string description, problems, sourceRows))
                {
                    continue;
                }

                if (!TryReadEnum(element, "removal", required: true, out string removalText, problems, sourceRows) ||
                    !TryParseRemoval(removalText, out UadRemovalTier removal))
                {
                    Add(problems, $"Row {sourceRows} ({id}) has an unknown removal tier.");
                    continue;
                }

                IReadOnlyList<string> dependencies = ReadStringArray(element, "dependencies", problems, sourceRows);
                IReadOnlyList<string> neededBy = ReadStringArray(element, "neededBy", problems, sourceRows);
                IReadOnlyList<string> labels = ReadStringArray(element, "labels", problems, sourceRows);

                if (problems.Count >= 20)
                {
                    break;
                }

                if (entries.ContainsKey(id))
                {
                    duplicates++;
                }

                entries[id] = new UadPackageEntry(id, list, description, dependencies, neededBy, labels, removal);
            }

            if (problems.Count > 0)
            {
                errors = problems;
                return false;
            }

            var packages = entries.Values
                .OrderBy(package => package.Id, StringComparer.Ordinal)
                .ToArray();

            catalog = new UadListCatalog(
                packages,
                origin,
                Hash(json),
                sourceRows,
                duplicates,
                origin switch
                {
                    UadListOrigin.Embedded => "Embedded UAD list",
                    UadListOrigin.Cached => "Cached UAD list",
                    _ => "Updated UAD list",
                });
            errors = Array.Empty<string>();
            return true;
        }
    }

    public static string Hash(string json) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();

    private static bool TryReadString(
        JsonElement element,
        string name,
        bool required,
        out string value,
        List<string> problems,
        int row)
    {
        value = string.Empty;

        if (!element.TryGetProperty(name, out JsonElement property) || property.ValueKind == JsonValueKind.Null)
        {
            if (required)
            {
                Add(problems, $"Row {row} is missing '{name}'.");
            }

            return !required;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            Add(problems, $"Row {row} has a non-string '{name}'.");
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return true;
    }

    private static bool TryReadEnum(
        JsonElement element,
        string name,
        bool required,
        out string value,
        List<string> problems,
        int row) =>
        TryReadString(element, name, required, out value, problems, row);

    private static IReadOnlyList<string> ReadStringArray(
        JsonElement element,
        string name,
        List<string> problems,
        int row)
    {
        if (!element.TryGetProperty(name, out JsonElement property) ||
            property.ValueKind == JsonValueKind.Null)
        {
            return Array.Empty<string>();
        }

        if (property.ValueKind != JsonValueKind.Array)
        {
            Add(problems, $"Row {row} has a non-array '{name}'.");
            return Array.Empty<string>();
        }

        var values = new List<string>();

        foreach (JsonElement item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                Add(problems, $"Row {row} has a non-string item in '{name}'.");
                return Array.Empty<string>();
            }

            values.Add(item.GetString() ?? string.Empty);
        }

        return values;
    }

    private static bool TryParseList(string value, out UadListKind list)
    {
        switch (value)
        {
            case "Aosp": list = UadListKind.Aosp; return true;
            case "Oem": list = UadListKind.Oem; return true;
            case "Carrier": list = UadListKind.Carrier; return true;
            case "Google": list = UadListKind.Google; return true;
            case "Misc": list = UadListKind.Misc; return true;
            case "Pending": list = UadListKind.Pending; return true;
            case "Unlisted": list = UadListKind.Unlisted; return true;
            default: list = default; return false;
        }
    }

    private static bool TryParseRemoval(string value, out UadRemovalTier removal)
    {
        switch (value)
        {
            case "Recommended": removal = UadRemovalTier.Recommended; return true;
            case "Advanced": removal = UadRemovalTier.Advanced; return true;
            case "Expert": removal = UadRemovalTier.Expert; return true;
            case "Unsafe": removal = UadRemovalTier.Unsafe; return true;
            case "Unlisted": removal = UadRemovalTier.Unlisted; return true;
            default: removal = default; return false;
        }
    }

    private static void Add(List<string> problems, string message)
    {
        if (problems.Count < 20)
        {
            problems.Add(message);
        }
    }
}
