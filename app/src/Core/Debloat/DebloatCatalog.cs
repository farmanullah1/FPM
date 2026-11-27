using System.Text.Json;
using System.Text.Json.Serialization;

namespace PCManager.Core.Debloat;

/// <summary>
/// Typed port of Win11Debloat's declarative data (MIT). The helper validates tweak ids against
/// this catalog; nothing here accepts free-form commands.
/// </summary>
public sealed class DebloatCatalog
{
    public string Version { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string GeneratedBy { get; set; } = string.Empty;
    public List<DebloatCategory> Categories { get; set; } = new();
    public List<DebloatTweak> Tweaks { get; set; } = new();
    public List<DebloatApp> Apps { get; set; } = new();
    public List<DebloatAppPreset> AppPresets { get; set; } = new();
    public List<ExcludedFeature> ExcludedFeatures { get; set; } = new();
}

public sealed class DebloatCategory
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class DebloatTweak
{
    public string Id { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Risk { get; set; } = string.Empty;
    public Dictionary<string, DebloatOsGate> Os { get; set; } = new();
    public bool RequiresReboot { get; set; }
    public bool RequiresExplorerRestart { get; set; }
    public string AppliesTo { get; set; } = string.Empty;
    public List<DebloatAction> Actions { get; set; } = new();
    public List<DebloatAction> Revert { get; set; } = new();
    public string RevertNote { get; set; } = string.Empty;
    public List<DebloatProbe> Detect { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public string ReferenceFile { get; set; } = string.Empty;
}

public sealed class DebloatOsGate
{
    public int? MinBuild { get; set; }
    public int? MaxBuild { get; set; }
}

public sealed class DebloatApp
{
    public string Id { get; set; } = string.Empty;
    public List<string> PackageIds { get; set; } = new();
    public string Recommendation { get; set; } = string.Empty;
    public bool SelectedByDefault { get; set; }
    public string RemovalMethod { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ReferenceFile { get; set; } = string.Empty;
}

public sealed class DebloatAppPreset
{
    public string Name { get; set; } = string.Empty;
    public List<string> AppIds { get; set; } = new();
}

public sealed class ExcludedFeature
{
    public string FeatureId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(RegistrySetAction), "RegistrySet")]
[JsonDerivedType(typeof(RegistryDeleteAction), "RegistryDelete")]
[JsonDerivedType(typeof(RegistryKeyDeleteAction), "RegistryKeyDelete")]
[JsonDerivedType(typeof(OptionalFeatureAction), "OptionalFeature")]
[JsonDerivedType(typeof(AppxRemoveAction), "AppxRemove")]
public abstract class DebloatAction
{
    public string Hive { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}

public sealed class RegistrySetAction : DebloatAction
{
    [JsonPropertyName("name")]
    public string RegistryName { get; set; } = string.Empty;

    public string ValueType { get; set; } = string.Empty;
    public JsonElement Value { get; set; }
}

public sealed class RegistryDeleteAction : DebloatAction
{
    [JsonPropertyName("name")]
    public string RegistryName { get; set; } = string.Empty;
}

public sealed class RegistryKeyDeleteAction : DebloatAction
{
}

public sealed class OptionalFeatureAction : DebloatAction
{
    [JsonPropertyName("name")]
    public string FeatureName { get; set; } = string.Empty;

    public bool Enable { get; set; }
}

public sealed class AppxRemoveAction : DebloatAction
{
    public string PackageId { get; set; } = string.Empty;
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(RegistryEqualsProbe), "RegistryEquals")]
[JsonDerivedType(typeof(RegistryAbsentProbe), "RegistryAbsent")]
[JsonDerivedType(typeof(RegistryKeyAbsentProbe), "RegistryKeyAbsent")]
[JsonDerivedType(typeof(OptionalFeatureProbe), "OptionalFeature")]
[JsonDerivedType(typeof(AppxAbsentProbe), "AppxAbsent")]
public abstract class DebloatProbe
{
    public string Hive { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}

public sealed class RegistryEqualsProbe : DebloatProbe
{
    [JsonPropertyName("name")]
    public string RegistryName { get; set; } = string.Empty;

    public string ValueType { get; set; } = string.Empty;
    public JsonElement Value { get; set; }
}

public sealed class RegistryAbsentProbe : DebloatProbe
{
    [JsonPropertyName("name")]
    public string RegistryName { get; set; } = string.Empty;
}

public sealed class RegistryKeyAbsentProbe : DebloatProbe
{
}

public sealed class OptionalFeatureProbe : DebloatProbe
{
    [JsonPropertyName("name")]
    public string FeatureName { get; set; } = string.Empty;

    public bool ExpectEnabled { get; set; }
}

public sealed class AppxAbsentProbe : DebloatProbe
{
    public string PackageId { get; set; } = string.Empty;
}
