using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>Read-only Store/MSIX listing and confirmation-gated current-user removal.</summary>
public interface IAppxPackageService
{
    Task<IReadOnlyList<AppxPackage>> GetPackagesAsync(CancellationToken cancellationToken = default);

    Task<OperationResult> UninstallAsync(AppxPackage package, CancellationToken cancellationToken = default);
}
