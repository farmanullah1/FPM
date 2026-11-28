namespace PCManager.Core.Android;

public enum UadListKind
{
    Aosp,
    Oem,
    Carrier,
    Google,
    Misc,
    Pending,
    Unlisted,
}

public enum UadRemovalTier
{
    Recommended,
    Advanced,
    Expert,
    Unsafe,
    Unlisted,
}

public enum UadListOrigin
{
    Embedded,
    Cached,
    Remote,
}

public sealed record UadPackageEntry(
    string Id,
    UadListKind List,
    string Description,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<string> NeededBy,
    IReadOnlyList<string> Labels,
    UadRemovalTier Removal)
{
    public string ListText => List.ToString();

    public string RemovalText => Removal.ToString();
}

public sealed record UadListCatalog(
    IReadOnlyList<UadPackageEntry> Packages,
    UadListOrigin Origin,
    string Sha256,
    int SourceRowCount,
    int DuplicateIdCount,
    string SourceLabel)
{
    public IReadOnlyDictionary<string, UadPackageEntry> ById { get; } =
        Packages.ToDictionary(package => package.Id, StringComparer.Ordinal);

    public string ShortHash => Sha256.Length >= 12 ? Sha256[..12] + "…" : Sha256;

    public int Count(UadListKind kind) => Packages.Count(package => package.List == kind);

    public int Count(UadRemovalTier tier) => Packages.Count(package => package.Removal == tier);
}

public sealed record UadListDiff(
    int Added,
    int Removed,
    int Changed,
    int DuplicateIds,
    IReadOnlyList<string> AddedIds,
    IReadOnlyList<string> RemovedIds,
    IReadOnlyList<string> ChangedIds)
{
    public bool HasChanges => Added > 0 || Removed > 0 || Changed > 0;

    public string Summary
    {
        get
        {
            string changes = HasChanges
                ? $"Added {Added}, removed {Removed}, changed {Changed}"
                : "No package changes";

            return DuplicateIds == 0
                ? changes + "."
                : changes + $" · {DuplicateIds} duplicate id(s) normalized last-wins.";
        }
    }

    public static UadListDiff Between(UadListCatalog previous, UadListCatalog next)
    {
        var previousIds = previous.Packages.Select(package => package.Id).ToHashSet(StringComparer.Ordinal);
        var nextIds = next.Packages.Select(package => package.Id).ToHashSet(StringComparer.Ordinal);
        var added = nextIds.Except(previousIds, StringComparer.Ordinal).OrderBy(id => id).ToArray();
        var removed = previousIds.Except(nextIds, StringComparer.Ordinal).OrderBy(id => id).ToArray();
        var changed = next.Packages
            .Where(package => previous.ById.TryGetValue(package.Id, out UadPackageEntry? old) &&
                              !Equivalent(old, package))
            .Select(package => package.Id)
            .OrderBy(id => id)
            .ToArray();

        return new UadListDiff(
            added.Length,
            removed.Length,
            changed.Length,
            next.DuplicateIdCount,
            added.Take(8).ToArray(),
            removed.Take(8).ToArray(),
            changed.Take(8).ToArray());
    }

    private static bool Equivalent(UadPackageEntry left, UadPackageEntry right) =>
        left.List == right.List &&
        left.Removal == right.Removal &&
        left.Description == right.Description &&
        left.Dependencies.SequenceEqual(right.Dependencies, StringComparer.Ordinal) &&
        left.NeededBy.SequenceEqual(right.NeededBy, StringComparer.Ordinal) &&
        left.Labels.SequenceEqual(right.Labels, StringComparer.Ordinal);
}

public enum UadListUpdateStatus
{
    Updated,
    AlreadyCurrent,
    Unavailable,
    Invalid,
    Failed,
}

public sealed record UadListUpdateResult(
    UadListUpdateStatus Status,
    string Message,
    UadListCatalog? Catalog,
    UadListDiff? Diff,
    string? Sha256);
