namespace PCManager.Core.Android;

public sealed record AndroidPackageSnapshotEntry(
    string PackageId,
    PackageState State);

public sealed record AndroidPackageSnapshot(
    string Id,
    DateTimeOffset CreatedAt,
    string DeviceSerialHash,
    string DeviceModel,
    int UserId,
    string UserName,
    IReadOnlyList<AndroidPackageSnapshotEntry> Packages);
