namespace PCManager.Core.Android;

public enum PackageListFilter
{
    All,
    System,
    Enabled,
    Disabled,
}

public enum PackageState
{
    Enabled,
    Disabled,
    Uninstalled,
}

public enum PackageAction
{
    Uninstall,
    Disable,
    Restore,
    Enable,
}

public enum PackageResultStatus
{
    Ok,
    Already,
    Failed,
    BlockedDenylist,
}

public enum PackageScanStatus
{
    Ready,
    Unavailable,
    Disconnected,
    Failed,
}

public enum PackageBatchStatus
{
    Completed,
    StoppedDisconnected,
    Cancelled,
    NotStarted,
}

public enum PackageDependencyKind
{
    DependsOn,
    NeededBy,
}

public sealed record AndroidPackage(
    string Id,
    PackageState State,
    bool IsSystem,
    UadPackageEntry? Metadata)
{
    public string ListText => Metadata?.List.ToString() ?? "Unlisted";

    public string RemovalText => Metadata?.Removal.ToString() ?? "Unlisted";

    public string Description => Metadata?.Description is { Length: > 0 } text
        ? text
        : "No description available.";

    public IReadOnlyList<string> Dependencies => Metadata?.Dependencies ?? Array.Empty<string>();

    public IReadOnlyList<string> NeededBy => Metadata?.NeededBy ?? Array.Empty<string>();

    public bool IsDenylisted()
        => PackageDenylist.Contains(Id) || Metadata?.Removal == UadRemovalTier.Unsafe;

    public bool IsSelectable(bool expertMode) =>
        expertMode || (Metadata is not null && Metadata.List != UadListKind.Unlisted);

    public string StateText => State switch
    {
        PackageState.Enabled => "Enabled",
        PackageState.Disabled => "Disabled",
        PackageState.Uninstalled => "Uninstalled",
        _ => "Unknown",
    };
}

public sealed record PackageScan(
    string Serial,
    int UserId,
    IReadOnlyList<AndroidPackage> Packages)
{
    public IReadOnlyDictionary<string, AndroidPackage> ById { get; } =
        Packages.ToDictionary(package => package.Id, StringComparer.Ordinal);

    public int SystemCount => Packages.Count(package => package.IsSystem);

    public int EnabledCount => Packages.Count(package => package.State == PackageState.Enabled);

    public int DisabledCount => Packages.Count(package => package.State == PackageState.Disabled);

    public int UninstalledCount => Packages.Count(package => package.State == PackageState.Uninstalled);
}

public sealed record PackageScanResult(
    PackageScanStatus Status,
    string Message,
    PackageScan? Scan);

public sealed record PackageActionRequest(string PackageId, PackageAction Action);

public sealed record PackageApplyOptions(
    bool DisableInsteadOfUninstall = false,
    bool ExpertMode = false,
    string? ExpertConfirmation = null);

public sealed record PackageActionResult(
    string PackageId,
    PackageAction RequestedAction,
    PackageAction Action,
    PackageState Before,
    PackageState? After,
    PackageResultStatus Status,
    string Message);

public sealed record PackageDependencyWarning(
    PackageDependencyKind Kind,
    string PackageId,
    IReadOnlyList<string> RelatedIds,
    string Message);

public sealed record PackageBatchResult(
    PackageBatchStatus Status,
    string Message,
    IReadOnlyList<PackageActionResult> Results,
    int RemainingCount,
    bool Verified,
    PackageScan? VerifiedScan,
    IReadOnlyList<PackageDependencyWarning> DependencyWarnings)
{
    public int OkCount => Results.Count(result => result.Status == PackageResultStatus.Ok);
    public int AlreadyCount => Results.Count(result => result.Status == PackageResultStatus.Already);
    public int FailedCount => Results.Count(result => result.Status == PackageResultStatus.Failed);
    public int BlockedCount => Results.Count(result => result.Status == PackageResultStatus.BlockedDenylist);

    public bool HasChanges => OkCount > 0;

    public bool HasFailures => FailedCount > 0 || BlockedCount > 0;
}
