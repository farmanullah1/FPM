using PCManager.Core.Android;

namespace PCManager.Services.Android;

public interface IPackageEngine
{
    Task<PackageScanResult> ScanAsync(
        AdbDevice device,
        int userId,
        CancellationToken cancellationToken = default);

    Task<PackageBatchResult> ApplyAsync(
        AdbDevice device,
        int userId,
        IReadOnlyCollection<PackageActionRequest> requests,
        PackageApplyOptions? options = null,
        CancellationToken cancellationToken = default);
}
