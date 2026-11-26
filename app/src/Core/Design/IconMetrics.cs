namespace PCManager.Core.Design;

/// <summary>Detail level an Icon3D draws, chosen from its rendered size (plan U2).</summary>
public enum IconSizeClass
{
    /// <summary>16-24 px: flat gradient face only, no extrusion.</summary>
    Small,

    /// <summary>28-52 px: extrusion and specular band.</summary>
    Medium,

    /// <summary>56 px and up: soft shadow, deeper extrusion, specular crescent and rim light.</summary>
    Large
}

/// <summary>
/// Shared Icon3D measurements and resource-key rules. Lives in Core so the mapping can be
/// unit-tested without a WPF dispatcher.
/// </summary>
public static class IconMetrics
{
    public const double SmallMaxSize = 28;
    public const double MediumMaxSize = 56;

    public const string KeyPrefix = "Icon3D.";

    /// <summary>Small below 28 px, Large from 56 px, Medium between.</summary>
    public static IconSizeClass ForSize(double size) => size switch
    {
        < SmallMaxSize => IconSizeClass.Small,
        < MediumMaxSize => IconSizeClass.Medium,
        _ => IconSizeClass.Large
    };

    /// <summary>Geometry resource key, e.g. Icon3D.Memory.Neutral.</summary>
    public static string GeometryKey(string name, string role) =>
        $"{KeyPrefix}{(string.IsNullOrWhiteSpace(role) ? name : $"{name}.{role}")}";

    /// <summary>Accepts "Memory" or "Icon3D.Memory" and returns "Memory".</summary>
    public static string NormalizeKey(string? key)
    {
        string value = key?.Trim() ?? string.Empty;

        return value.StartsWith(KeyPrefix, StringComparison.Ordinal)
            ? value[KeyPrefix.Length..]
            : value;
    }

    /// <summary>Automation label for an icon that has no visible label of its own.</summary>
    public static string AutomationName(string? key)
    {
        string name = NormalizeKey(key);
        return name.Length == 0 ? "Icon" : string.Concat(name.Select((c, i) => i > 0 && char.IsUpper(c) ? $" {c}" : c.ToString()));
    }
}
