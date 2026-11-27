using System.Text.Json;

namespace PCManager.Core.Debloat;

/// <summary>
/// Structural and safety validation for the ported catalog (D1). Kept free of Windows APIs so the
/// data itself is testable on any machine.
/// </summary>
public static class DebloatCatalogValidator
{
    public static readonly string[] AllowedHives = { "HKCU", "HKLM", "HKU" };

    /// <summary>Registry roots the catalog may touch; HKU is limited to the Default profile.</summary>
    public static readonly string[] AllowedPathRoots = { "software", "system", "control panel" };

    public static readonly string[] AllowedRisks = { "Low", "Medium", "High" };

    public static readonly string[] AllowedAppliesTo = { "CurrentUser", "AllUsers", "DefaultUser" };

    public static readonly string[] AllowedValueTypes =
        { "DWord", "QWord", "String", "ExpandString", "MultiString", "Binary" };

    public static readonly string[] AllowedRecommendations = { "safe", "optional", "unsafe" };

    /// <summary>Packages FPM must never offer for removal (Windows core / security).</summary>
    public static readonly string[] ProtectedPackages =
    {
        "Microsoft.WindowsStore",
        "Microsoft.SecHealthUI",
        "Microsoft.Windows.ShellExperienceHost",
        "Microsoft.Windows.StartMenuExperienceHost",
        "Microsoft.DesktopAppInstaller",
        "Microsoft.VCLibs.140.00",
        "Microsoft.UI.Xaml",
    };

    public static IReadOnlyList<string> Validate(DebloatCatalog catalog)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(catalog.Version))
        {
            errors.Add("catalog.version is required");
        }

        if (catalog.Tweaks.Count == 0)
        {
            errors.Add("catalog.tweaks is empty");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (DebloatTweak tweak in catalog.Tweaks)
        {
            if (!ids.Add(tweak.Id))
            {
                errors.Add($"duplicate tweak id '{tweak.Id}'");
            }

            ValidateTweak(tweak, errors);
        }

        var appIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (DebloatApp app in catalog.Apps)
        {
            if (!appIds.Add(app.Id))
            {
                errors.Add($"duplicate app id '{app.Id}'");
            }

            if (!AllowedRecommendations.Contains(app.Recommendation))
            {
                errors.Add($"app '{app.Id}' has an unknown recommendation");
            }

            foreach (string packageId in app.PackageIds.Prepend(app.Id))
            {
                if (IsProtected(packageId))
                {
                    errors.Add($"protected package '{packageId}' must never be offered");
                }
            }
        }

        foreach (DebloatAppPreset preset in catalog.AppPresets)
        {
            foreach (string appId in preset.AppIds.Where(id => !appIds.Contains(id)))
            {
                errors.Add($"preset '{preset.Name}' references unknown app '{appId}'");
            }
        }

        return errors;
    }

    private static void ValidateTweak(DebloatTweak tweak, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(tweak.Id) || !tweak.Id.StartsWith("win11debloat.", StringComparison.Ordinal))
        {
            errors.Add($"tweak id '{tweak.Id}' must use the win11debloat. prefix");
        }

        if (!AllowedRisks.Contains(tweak.Risk))
        {
            errors.Add($"tweak '{tweak.Id}' has risk '{tweak.Risk}'");
        }

        if (!AllowedAppliesTo.Contains(tweak.AppliesTo))
        {
            errors.Add($"tweak '{tweak.Id}' has appliesTo '{tweak.AppliesTo}'");
        }

        if (tweak.Actions.Count == 0)
        {
            errors.Add($"tweak '{tweak.Id}' has no actions");
        }

        if (tweak.Detect.Count == 0)
        {
            errors.Add($"tweak '{tweak.Id}' has no detect probe");
        }

        if (tweak.Revert.Count == 0 && string.IsNullOrWhiteSpace(tweak.RevertNote))
        {
            errors.Add($"tweak '{tweak.Id}' needs a revert or a revert note");
        }

        if (tweak.ReferenceFile.Length == 0)
        {
            errors.Add($"tweak '{tweak.Id}' has no referenceFile");
        }

        foreach ((string os, DebloatOsGate gate) in tweak.Os)
        {
            if (os is not ("win10" or "win11"))
            {
                errors.Add($"tweak '{tweak.Id}' has unknown os key '{os}'");
            }

            if (gate.MinBuild is { } min && min is < 10240 or > 40000)
            {
                errors.Add($"tweak '{tweak.Id}' has an implausible minBuild {min}");
            }

            if (gate.MinBuild is { } low && gate.MaxBuild is { } high && low > high)
            {
                errors.Add($"tweak '{tweak.Id}' minBuild {low} exceeds maxBuild {high}");
            }
        }

        foreach (DebloatAction action in tweak.Actions.Concat(tweak.Revert))
        {
            ValidateAction(tweak.Id, action, errors);
        }

        foreach (DebloatProbe probe in tweak.Detect)
        {
            ValidateProbe(tweak.Id, probe, errors);
        }
    }

    private static void ValidateAction(string tweakId, DebloatAction action, List<string> errors)
    {
        switch (action)
        {
            case OptionalFeatureAction feature:
                if (string.IsNullOrWhiteSpace(feature.FeatureName))
                {
                    errors.Add($"tweak '{tweakId}' has an optional feature without a name");
                }

                return;
            case AppxRemoveAction appx:
                if (IsProtected(appx.PackageId))
                {
                    errors.Add($"tweak '{tweakId}' removes protected package '{appx.PackageId}'");
                }

                return;
        }

        ValidateRegistryPath(tweakId, action.Hive, action.Path, errors);

        if (action is RegistrySetAction set && !AllowedValueTypes.Contains(set.ValueType))
        {
            errors.Add($"tweak '{tweakId}' has value type '{set.ValueType}'");
        }

        if (action is RegistrySetAction { Value: { ValueKind: JsonValueKind.Undefined } })
        {
            errors.Add($"tweak '{tweakId}' has a registry value without data");
        }
    }

    private static void ValidateProbe(string tweakId, DebloatProbe probe, List<string> errors)
    {
        switch (probe)
        {
            case OptionalFeatureProbe feature:
                if (string.IsNullOrWhiteSpace(feature.FeatureName))
                {
                    errors.Add($"tweak '{tweakId}' has a feature probe without a name");
                }

                return;
            case AppxAbsentProbe appx:
                if (string.IsNullOrWhiteSpace(appx.PackageId))
                {
                    errors.Add($"tweak '{tweakId}' has an appx probe without a package id");
                }

                return;
        }

        ValidateRegistryPath(tweakId, probe.Hive, probe.Path, errors);
    }

    private static void ValidateRegistryPath(string tweakId, string hive, string path, List<string> errors)
    {
        if (!AllowedHives.Contains(hive))
        {
            errors.Add($"tweak '{tweakId}' uses hive '{hive}'");
            return;
        }

        if (hive == "HKU" && !path.StartsWith("Default", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"tweak '{tweakId}' touches HKU outside the Default profile");
            return;
        }

        string root = path.Split('\\')[0];
        if (!AllowedPathRoots.Contains(root, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add($"tweak '{tweakId}' path root '{root}' is outside the allow-list");
        }
    }

    public static bool IsProtectedPackage(string packageId) => IsProtected(packageId);

    private static bool IsProtected(string packageId) =>
        ProtectedPackages.Any(protectedId =>
            string.Equals(packageId, protectedId, StringComparison.OrdinalIgnoreCase) ||
            packageId.StartsWith(protectedId + ".", StringComparison.OrdinalIgnoreCase));
}
