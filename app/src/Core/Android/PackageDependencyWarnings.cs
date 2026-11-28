namespace PCManager.Core.Android;

public static class PackageDependencyWarnings
{
    public static IReadOnlyList<PackageDependencyWarning> Build(
        IEnumerable<AndroidPackage> selectedPackages)
    {
        var warnings = new List<PackageDependencyWarning>();

        foreach (AndroidPackage package in selectedPackages)
        {
            Add(warnings, package, PackageDependencyKind.DependsOn, package.Dependencies,
                "requires");
            Add(warnings, package, PackageDependencyKind.NeededBy, package.NeededBy,
                "is needed by");
        }

        return warnings;
    }

    private static void Add(
        ICollection<PackageDependencyWarning> warnings,
        AndroidPackage package,
        PackageDependencyKind kind,
        IReadOnlyList<string> related,
        string relationship)
    {
        string[] ids = related
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (ids.Length == 0)
        {
            return;
        }

        warnings.Add(new PackageDependencyWarning(
            kind,
            package.Id,
            ids,
            $"{package.Id} {relationship} {string.Join(", ", ids)}; review the related packages " +
            "before applying this batch."));
    }
}
