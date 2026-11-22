using PCManager.Core.Models;

namespace PCManager.Services.Abstractions;

/// <summary>Reads installed Win32 applications from the registry and starts their uninstallers.</summary>
public interface IInstalledAppService
{
    Task<IReadOnlyList<InstalledApp>> GetInstalledAppsAsync(CancellationToken cancellationToken = default);

    Task<OperationResult> UninstallAsync(InstalledApp app, CancellationToken cancellationToken = default);
}
