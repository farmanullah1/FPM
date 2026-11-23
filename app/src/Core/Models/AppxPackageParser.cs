using System.Text.Json;

namespace PCManager.Core.Models;

/// <summary>
/// Parses the compact JSON emitted by Get-AppxPackage. Kept free of process code so it can be
/// fixture-tested without querying the machine.
/// </summary>
public static class AppxPackageParser
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    public static IReadOnlyList<AppxPackage> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<AppxPackage>();
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json, DocumentOptions);
            JsonElement root = document.RootElement;

            IEnumerable<JsonElement> elements = root.ValueKind switch
            {
                JsonValueKind.Array => root.EnumerateArray(),
                JsonValueKind.Object => new[] { root },
                _ => Array.Empty<JsonElement>()
            };

            var packages = new List<AppxPackage>();

            foreach (JsonElement element in elements)
            {
                string fullName = ReadString(element, "PackageFullName");
                string name = ReadString(element, "Name");

                if (string.IsNullOrWhiteSpace(fullName) && string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var package = new AppxPackage
                {
                    Name = string.IsNullOrWhiteSpace(name) ? fullName : name,
                    PackageFullName = fullName,
                    Version = ReadString(element, "Version"),
                    Publisher = ReadString(element, "Publisher"),
                    InstallLocation = ReadString(element, "InstallLocation"),
                    SignatureKind = ReadSignatureKind(element),
                    IsFramework = ReadBoolean(element, "IsFramework"),
                    IsResourcePackage = ReadBoolean(element, "IsResourcePackage")
                };

                if (package.CanUninstall)
                {
                    packages.Add(package);
                }
            }

            return packages
                .OrderBy(package => package.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        catch (JsonException)
        {
            return Array.Empty<AppxPackage>();
        }
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property))
        {
            return string.Empty;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString() ?? string.Empty,
            JsonValueKind.Number => property.ToString(),
            _ => string.Empty
        };
    }

    private static string ReadSignatureKind(JsonElement element)
    {
        if (!element.TryGetProperty("SignatureKind", out JsonElement property))
        {
            return string.Empty;
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            return property.GetString() ?? string.Empty;
        }

        // Raw ConvertTo-Json emits the SignatureKind enum as a number. The service asks for
        // ToString(), but map the numbers as well so a system package can never slip through.
        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out int kind))
        {
            return kind switch
            {
                1 => "Developer",
                2 => "Enterprise",
                3 => "Store",
                4 => "System",
                _ => "None"
            };
        }

        return string.Empty;
    }

    private static bool ReadBoolean(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out JsonElement property) &&
           property.ValueKind == JsonValueKind.True;
}
